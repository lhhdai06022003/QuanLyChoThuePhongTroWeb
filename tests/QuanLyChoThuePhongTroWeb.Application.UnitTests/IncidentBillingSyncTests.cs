using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Security;
using QuanLyChoThuePhongTroWeb.Application.Common.Enums;
using QuanLyChoThuePhongTroWeb.Application.Common.Security;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Services;
using QuanLyChoThuePhongTroWeb.Application.Features.YeuCauSuCos.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Features.YeuCauSuCos.Services;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    public class IncidentBillingSyncTests
    {
        private class FakeApplicationTransaction : IApplicationTransaction
        {
            public bool Committed { get; private set; }
            public bool RolledBack { get; private set; }
            public Task CommitAsync(CancellationToken cancellationToken = default) { Committed = true; return Task.CompletedTask; }
            public Task RollbackAsync(CancellationToken cancellationToken = default) { RolledBack = true; return Task.CompletedTask; }
            public void Dispose() { }
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }

        private class FakeUnitOfWork : IUnitOfWork
        {
            public FakeApplicationTransaction LastTx { get; private set; } = new();
            public bool SaveChangesCalled { get; private set; }
            public bool ThrowOnSave { get; set; }
            public Task<IApplicationTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
            {
                LastTx = new FakeApplicationTransaction();
                return Task.FromResult<IApplicationTransaction>(LastTx);
            }

            public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            {
                SaveChangesCalled = true;
                if (ThrowOnSave)
                {
                    throw new InvalidOperationException("SECRET_DB_ERROR_SUCO");
                }
                return Task.FromResult(1);
            }
        }

        private class FakeEmployeeAccessService : IEmployeeAccessService
        {
            public HashSet<(int ActorId, int BranchId, string Action)> Permissions { get; } = new();

            public Task<bool> CanPerformAsync(int actorId, int branchId, string actionCode, CancellationToken ct = default)
            {
                return Task.FromResult(Permissions.Contains((actorId, branchId, actionCode)));
            }

            public Task<EmployeeAccessScope?> GetScopeAsync(int actorId, CancellationToken ct = default)
            {
                return Task.FromResult<EmployeeAccessScope?>(null);
            }
        }

        private class FakeYeuCauSuCoStore : IYeuCauSuCoStore
        {
            public Dictionary<int, YeuCauSuCo> Incidents { get; } = new();

            public Task<List<YeuCauSuCo>> GetAllAsync(int? chiNhanhId, TrangThaiSuCo? trangThai, int? soThang = 6, CancellationToken cancellationToken = default)
                => Task.FromResult(Incidents.Values.ToList());

            public Task<List<YeuCauSuCo>> GetByNguoiThueAsync(int nguoiThueId, CancellationToken cancellationToken = default)
                => Task.FromResult(Incidents.Values.Where(x => x.NguoiThueId == nguoiThueId).ToList());

            public Task<YeuCauSuCo?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
            {
                Incidents.TryGetValue(id, out var item);
                return Task.FromResult(item);
            }

            public void Add(YeuCauSuCo model) => Incidents[model.Id] = model;
            public void Update(YeuCauSuCo model) => Incidents[model.Id] = model;
        }

        private class FakeInvoiceIssuanceStore : IInvoiceIssuanceStore
        {
            public List<int> LockedContractIds { get; } = new();
            public Dictionary<(int PhongTroId, int NguoiThueId, int Thang, int Nam), HoaDon> Invoices { get; } = new();
            public List<YeuCauSuCo> BillableIncidents { get; set; } = new();
            public List<int> ContractIds { get; set; } = new();

            public Task<IReadOnlyList<int>> LockContractsAsync(IReadOnlyList<int> hopDongIds, CancellationToken ct = default)
            {
                LockedContractIds.AddRange(hopDongIds);
                return Task.FromResult(hopDongIds);
            }

            public Task<HoaDon?> GetInvoiceForUpdateAsync(int hoaDonId, CancellationToken ct = default) => Task.FromResult<HoaDon?>(null);
            public Task<int> CountCancelledInvoicesAsync(int hopDongId, int thang, int nam, CancellationToken ct = default) => Task.FromResult(0);
            public Task<bool> HasOtherActiveInvoiceAsync(int hopDongId, int thang, int nam, int excludeHoaDonId, CancellationToken ct = default) => Task.FromResult(false);
            public Task AddInvoiceAsync(HoaDon hoaDon, CancellationToken ct = default) => Task.CompletedTask;

            public Task<IReadOnlyList<YeuCauSuCo>> GetBillableIncidentsInPeriodAsync(IReadOnlyList<int> roomIds, DateTime startUtc, DateTime nextMonthStartUtc, CancellationToken ct = default)
            {
                var filtered = BillableIncidents
                    .Where(x => roomIds.Contains(x.PhongTroId) && x.NgayXuLy >= startUtc && x.NgayXuLy < nextMonthStartUtc)
                    .ToList();
                return Task.FromResult<IReadOnlyList<YeuCauSuCo>>(filtered);
            }

            public Task<HoaDon?> GetActiveInvoiceForTenantRoomPeriodForUpdateAsync(int phongTroId, int nguoiThueId, int thang, int nam, CancellationToken ct = default)
            {
                Invoices.TryGetValue((phongTroId, nguoiThueId, thang, nam), out var inv);
                return Task.FromResult(inv);
            }

            public Task<(int? MeterPeriodId, int ChiNhanhId)?> GetInvoiceLockTargetsAsync(int hoaDonId, CancellationToken ct = default) => Task.FromResult<(int? MeterPeriodId, int ChiNhanhId)?>(null);
            public Task<TrangThaiGhiNhan?> LockMeterPeriodAsync(int meterPeriodId, CancellationToken ct = default) => Task.FromResult<TrangThaiGhiNhan?>(null);

            public Task<IReadOnlyList<int>> GetContractIdsForTenantRoomAsync(int phongTroId, int nguoiThueId, CancellationToken ct = default)
            {
                return Task.FromResult<IReadOnlyList<int>>(ContractIds);
            }
        }

        private (YeuCauSuCoService service, FakeYeuCauSuCoStore suCoStore, FakeInvoiceIssuanceStore issuanceStore, FakeEmployeeAccessService access, FakeUnitOfWork uow) CreateService()
        {
            var suCoStore = new FakeYeuCauSuCoStore();
            var issuanceStore = new FakeInvoiceIssuanceStore();
            var access = new FakeEmployeeAccessService();
            var uow = new FakeUnitOfWork();
            var logger = NullLogger<YeuCauSuCoService>.Instance;

            var service = new YeuCauSuCoService(suCoStore, uow, access, issuanceStore, logger);
            return (service, suCoStore, issuanceStore, access, uow);
        }

        private YeuCauSuCo SetupIncident(FakeYeuCauSuCoStore store, FakeEmployeeAccessService access, int actorId = 1, int branchId = 1)
        {
            access.Permissions.Add((actorId, branchId, EmployeeActionCodes.InvoiceDraft));

            var incident = new YeuCauSuCo
            {
                Id = 10,
                PhongTroId = 101,
                NguoiThueId = 201,
                TieuDe = "Sửa bóng đèn",
                TrangThai = TrangThaiSuCo.DangXuLy,
                CongVaoHoaDon = true,
                ChiPhiSuaChua = 100000m,
                NgayXuLy = new DateTime(2026, 9, 10, 10, 0, 0, DateTimeKind.Utc),
                PhongTro = new PhongTro
                {
                    PhongTroId = 101,
                    ChiNhanhId = branchId,
                    SoPhong = "P101"
                }
            };
            store.Add(incident);
            return incident;
        }

        [Fact]
        public async Task UpdateStatus_ResaveWhileDaHoanThanh_KeepsNgayXuLy()
        {
            var (service, suCoStore, _, access, _) = CreateService();
            var incident = SetupIncident(suCoStore, access);
            incident.TrangThai = TrangThaiSuCo.DaHoanThanh;
            var originalNgayXuLy = new DateTime(2026, 9, 10, 8, 30, 0, DateTimeKind.Utc);
            incident.NgayXuLy = originalNgayXuLy;

            var result = await service.UpdateStatusAsync(
                id: incident.Id,
                trangThai: AppTrangThaiSuCo.DaHoanThanh,
                chiPhi: 150000m,
                congVaoHoaDon: true,
                lyDoTuChoi: null,
                ghiChuAdmin: "Cập nhật chi phí",
                actorId: 1);

            Assert.True(result.Success);
            Assert.Equal(originalNgayXuLy, incident.NgayXuLy);
            Assert.Equal(150000m, incident.ChiPhiSuaChua);
        }

        [Fact]
        public async Task UpdateStatus_TransitionToDangXuLy_SetsNgayXuLy()
        {
            var (service, suCoStore, _, access, _) = CreateService();
            var incident = SetupIncident(suCoStore, access);
            incident.TrangThai = TrangThaiSuCo.ChoTiepNhan;
            incident.NgayXuLy = null;

            var before = DateTime.UtcNow;
            var result = await service.UpdateStatusAsync(
                id: incident.Id,
                trangThai: AppTrangThaiSuCo.DangXuLy,
                chiPhi: 0m,
                congVaoHoaDon: false,
                lyDoTuChoi: null,
                ghiChuAdmin: null,
                actorId: 1);

            Assert.True(result.Success);
            Assert.NotNull(incident.NgayXuLy);
            Assert.InRange(incident.NgayXuLy.Value, before, DateTime.UtcNow);
        }

        [Fact]
        public async Task UpdateStatus_TransitionToDaHoanThanh_OverwritesNgayXuLyWithCompletionTime()
        {
            var (service, suCoStore, _, access, _) = CreateService();
            var incident = SetupIncident(suCoStore, access);
            incident.TrangThai = TrangThaiSuCo.DangXuLy;
            incident.NgayXuLy = new DateTime(2026, 8, 28, 10, 0, 0, DateTimeKind.Utc);

            var before = DateTime.UtcNow;
            var result = await service.UpdateStatusAsync(
                id: incident.Id,
                trangThai: AppTrangThaiSuCo.DaHoanThanh,
                chiPhi: 200000m,
                congVaoHoaDon: true,
                lyDoTuChoi: null,
                ghiChuAdmin: "Đã hoàn thành",
                actorId: 1);

            Assert.True(result.Success);
            Assert.NotNull(incident.NgayXuLy);
            Assert.InRange(incident.NgayXuLy.Value, before, DateTime.UtcNow);
        }

        [Fact]
        public async Task UpdateStatus_DangXuLyWithFlag_IsNotBilled()
        {
            var (service, suCoStore, issuanceStore, access, _) = CreateService();
            var incident = SetupIncident(suCoStore, access);
            incident.TrangThai = TrangThaiSuCo.ChoTiepNhan;
            incident.NgayXuLy = null;

            // Chuyển sang DangXuLy nhưng bật CongVaoHoaDon = true, chi phí 200k
            var result = await service.UpdateStatusAsync(
                id: incident.Id,
                trangThai: AppTrangThaiSuCo.DangXuLy,
                chiPhi: 200000m,
                congVaoHoaDon: true,
                lyDoTuChoi: null,
                ghiChuAdmin: null,
                actorId: 1);

            Assert.True(result.Success);
            // Vì trạng thái là DangXuLy nên chưa tính phí (billed cost = 0), không đụng vào khóa hóa đơn
            Assert.Empty(issuanceStore.LockedContractIds);
        }

        [Fact]
        public async Task UpdateStatus_ChangeCost_WhenMonthInvoiceIsNhap_RecalculatesIncidentLinesAndTotal()
        {
            var (service, suCoStore, issuanceStore, access, _) = CreateService();
            var incident = SetupIncident(suCoStore, access);
            incident.TrangThai = TrangThaiSuCo.DaHoanThanh;
            incident.ChiPhiSuaChua = 100000m;
            incident.CongVaoHoaDon = true;
            incident.NgayXuLy = new DateTime(2026, 9, 10, 10, 0, 0, DateTimeKind.Utc);

            issuanceStore.ContractIds.Add(50);
            issuanceStore.BillableIncidents.Add(incident);

            var hoaDon = new HoaDon
            {
                HoaDonId = 1,
                HopDongId = 50,
                Thang = 9,
                Nam = 2026,
                TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.Nhap,
                TongTien = 2100000m,
                ChiTietHoaDonDichVus = new List<ChiTietHoaDon>
                {
                    new ChiTietHoaDon { ChiTietHoaDonId = 1, TenDichVu = "Tiền phòng", TongTien = 2000000m, DonGia = 2000000m, SoLuong = 1, DichVuId = 1 },
                    new ChiTietHoaDon { ChiTietHoaDonId = 2, TenDichVu = InvoiceLineNames.SuCoPrefix + "Sửa bóng đèn", TongTien = 100000m, DonGia = 100000m, SoLuong = 1, DichVuId = null }
                }
            };
            issuanceStore.Invoices[(incident.PhongTroId, incident.NguoiThueId, 9, 2026)] = hoaDon;

            var result = await service.UpdateStatusAsync(
                id: incident.Id,
                trangThai: AppTrangThaiSuCo.DaHoanThanh,
                chiPhi: 250000m,
                congVaoHoaDon: true,
                lyDoTuChoi: null,
                ghiChuAdmin: "Tăng chi phí",
                actorId: 1);

            Assert.True(result.Success);
            Assert.Contains(50, issuanceStore.LockedContractIds);
            var activeLines = hoaDon.ChiTietHoaDonDichVus.Where(x => !x.IsDeleted).ToList();
            var suCoLine = activeLines.Single(x => x.TenDichVu.StartsWith(InvoiceLineNames.SuCoPrefix));
            Assert.Equal(250000m, suCoLine.TongTien);
            Assert.Equal(2250000m, hoaDon.TongTien);
        }

        [Theory]
        [InlineData(TrangThaiPhatHanhHoaDon.ChoDuyet)]
        [InlineData(TrangThaiPhatHanhHoaDon.DaChot)]
        [InlineData(TrangThaiPhatHanhHoaDon.DaGui)]
        public async Task UpdateStatus_ChangeCost_WhenMonthInvoiceIsChoDuyetOrDaChot_ReturnsFail_NothingSaved(TrangThaiPhatHanhHoaDon invoiceStatus)
        {
            var (service, suCoStore, issuanceStore, access, _) = CreateService();
            var incident = SetupIncident(suCoStore, access);
            incident.TrangThai = TrangThaiSuCo.DaHoanThanh;
            incident.ChiPhiSuaChua = 100000m;
            incident.CongVaoHoaDon = true;
            incident.NgayXuLy = new DateTime(2026, 9, 10, 10, 0, 0, DateTimeKind.Utc);

            issuanceStore.ContractIds.Add(50);
            var hoaDon = new HoaDon
            {
                HoaDonId = 1,
                HopDongId = 50,
                Thang = 9,
                Nam = 2026,
                TrangThaiPhatHanh = invoiceStatus,
                TongTien = 2100000m
            };
            issuanceStore.Invoices[(incident.PhongTroId, incident.NguoiThueId, 9, 2026)] = hoaDon;

            var result = await service.UpdateStatusAsync(
                id: incident.Id,
                trangThai: AppTrangThaiSuCo.DaHoanThanh,
                chiPhi: 250000m,
                congVaoHoaDon: true,
                lyDoTuChoi: null,
                ghiChuAdmin: "Tăng chi phí",
                actorId: 1);

            Assert.False(result.Success);
            Assert.Equal("Hóa đơn tháng 9/2026 đã gửi duyệt hoặc đã chốt. Admin cần trả lại hoặc hủy hóa đơn trước khi sửa chi phí sự cố.", result.Message);
            Assert.Equal(100000m, incident.ChiPhiSuaChua);
        }

        [Fact]
        public async Task UpdateStatus_NoInvoiceYet_SavesNormally()
        {
            var (service, suCoStore, issuanceStore, access, _) = CreateService();
            var incident = SetupIncident(suCoStore, access);
            incident.TrangThai = TrangThaiSuCo.DaHoanThanh;
            incident.ChiPhiSuaChua = 100000m;
            incident.CongVaoHoaDon = true;
            incident.NgayXuLy = new DateTime(2026, 9, 10, 10, 0, 0, DateTimeKind.Utc);

            // Không có hóa đơn tháng 9
            var result = await service.UpdateStatusAsync(
                id: incident.Id,
                trangThai: AppTrangThaiSuCo.DaHoanThanh,
                chiPhi: 250000m,
                congVaoHoaDon: true,
                lyDoTuChoi: null,
                ghiChuAdmin: "Tăng chi phí",
                actorId: 1);

            Assert.True(result.Success);
            Assert.Equal(250000m, incident.ChiPhiSuaChua);
        }

        [Fact]
        public async Task UpdateStatus_ToggleOffFlag_RemovesIncidentLineFromDraft()
        {
            var (service, suCoStore, issuanceStore, access, _) = CreateService();
            var incident = SetupIncident(suCoStore, access);
            incident.TrangThai = TrangThaiSuCo.DaHoanThanh;
            incident.ChiPhiSuaChua = 200000m;
            incident.CongVaoHoaDon = true;
            incident.NgayXuLy = new DateTime(2026, 9, 10, 10, 0, 0, DateTimeKind.Utc);

            issuanceStore.ContractIds.Add(50);
            issuanceStore.BillableIncidents.Add(incident);

            var hoaDon = new HoaDon
            {
                HoaDonId = 1,
                HopDongId = 50,
                Thang = 9,
                Nam = 2026,
                TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.Nhap,
                TongTien = 2200000m,
                ChiTietHoaDonDichVus = new List<ChiTietHoaDon>
                {
                    new ChiTietHoaDon { ChiTietHoaDonId = 1, TenDichVu = "Tiền phòng", TongTien = 2000000m, DonGia = 2000000m, SoLuong = 1, DichVuId = 1 },
                    new ChiTietHoaDon { ChiTietHoaDonId = 2, TenDichVu = InvoiceLineNames.SuCoPrefix + "Sửa bóng đèn", TongTien = 200000m, DonGia = 200000m, SoLuong = 1, DichVuId = null }
                }
            };
            issuanceStore.Invoices[(incident.PhongTroId, incident.NguoiThueId, 9, 2026)] = hoaDon;

            var result = await service.UpdateStatusAsync(
                id: incident.Id,
                trangThai: AppTrangThaiSuCo.DaHoanThanh,
                chiPhi: 200000m,
                congVaoHoaDon: false, // Tắt cờ
                lyDoTuChoi: null,
                ghiChuAdmin: "Không cộng vào hóa đơn",
                actorId: 1);

            Assert.True(result.Success);
            var activeLines = hoaDon.ChiTietHoaDonDichVus.Where(x => !x.IsDeleted).ToList();
            Assert.DoesNotContain(activeLines, x => x.TenDichVu.StartsWith(InvoiceLineNames.SuCoPrefix));
            Assert.Equal(2000000m, hoaDon.TongTien);
        }

        [Fact]
        public async Task UpdateStatus_DeniedForStaffOfOtherBranch()
        {
            var (service, suCoStore, _, access, _) = CreateService();
            var incident = SetupIncident(suCoStore, access, actorId: 2, branchId: 1);
            // Staff 20 chỉ có quyền ở CN 2
            access.Permissions.Add((20, 2, EmployeeActionCodes.InvoiceDraft));

            var result = await service.UpdateStatusAsync(
                id: incident.Id,
                trangThai: AppTrangThaiSuCo.DangXuLy,
                chiPhi: 0m,
                congVaoHoaDon: false,
                lyDoTuChoi: null,
                ghiChuAdmin: null,
                actorId: 20);

            Assert.False(result.Success);
            Assert.Contains("không có quyền", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task UpdateStatus_DoesNotChangeOtherInvoiceLines()
        {
            var (service, suCoStore, issuanceStore, access, _) = CreateService();
            var incident = SetupIncident(suCoStore, access);
            incident.TrangThai = TrangThaiSuCo.DaHoanThanh;
            incident.ChiPhiSuaChua = 100000m;
            incident.CongVaoHoaDon = true;
            incident.NgayXuLy = new DateTime(2026, 9, 10, 10, 0, 0, DateTimeKind.Utc);

            issuanceStore.ContractIds.Add(50);
            issuanceStore.BillableIncidents.Add(incident);

            var hoaDon = new HoaDon
            {
                HoaDonId = 1,
                HopDongId = 50,
                Thang = 9,
                Nam = 2026,
                TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.Nhap,
                TongTien = 2400000m,
                ChiTietHoaDonDichVus = new List<ChiTietHoaDon>
                {
                    new ChiTietHoaDon { ChiTietHoaDonId = 1, TenDichVu = "Tiền phòng", TongTien = 2000000m, DonGia = 2000000m, SoLuong = 1, DichVuId = 1 },
                    new ChiTietHoaDon { ChiTietHoaDonId = 2, TenDichVu = "Điện: 100 x 3000", TongTien = 300000m, DonGia = 3000m, SoLuong = 100, DichVuId = 2 },
                    new ChiTietHoaDon { ChiTietHoaDonId = 3, TenDichVu = InvoiceLineNames.SuCoPrefix + "Sửa bóng đèn", TongTien = 100000m, DonGia = 100000m, SoLuong = 1, DichVuId = null }
                }
            };
            issuanceStore.Invoices[(incident.PhongTroId, incident.NguoiThueId, 9, 2026)] = hoaDon;

            var result = await service.UpdateStatusAsync(
                id: incident.Id,
                trangThai: AppTrangThaiSuCo.DaHoanThanh,
                chiPhi: 250000m,
                congVaoHoaDon: true,
                lyDoTuChoi: null,
                ghiChuAdmin: "Tăng chi phí",
                actorId: 1);

            Assert.True(result.Success);
            var roomLine = hoaDon.ChiTietHoaDonDichVus.Single(x => x.TenDichVu == "Tiền phòng");
            Assert.False(roomLine.IsDeleted);
            Assert.Equal(2000000m, roomLine.TongTien);

            var electricLine = hoaDon.ChiTietHoaDonDichVus.Single(x => x.TenDichVu == "Điện: 100 x 3000");
            Assert.False(electricLine.IsDeleted);
            Assert.Equal(300000m, electricLine.TongTien);

            Assert.Equal(2550000m, hoaDon.TongTien);
        }

        private static HoaDon CreateSeptemberInvoice(TrangThaiPhatHanhHoaDon status, bool withIncidentLine)
        {
            var lines = new List<ChiTietHoaDon>
            {
                new ChiTietHoaDon { ChiTietHoaDonId = 1, TenDichVu = "Tiền phòng", TongTien = 2000000m, DonGia = 2000000m, SoLuong = 1, DichVuId = 1 }
            };
            if (withIncidentLine)
            {
                lines.Add(new ChiTietHoaDon { ChiTietHoaDonId = 2, TenDichVu = InvoiceLineNames.SuCoPrefix + "Sửa bóng đèn", TongTien = 100000m, DonGia = 100000m, SoLuong = 1, DichVuId = null });
            }
            return new HoaDon
            {
                HoaDonId = 1,
                HopDongId = 50,
                Thang = 9,
                Nam = 2026,
                TrangThaiPhatHanh = status,
                TongTien = lines.Sum(x => x.TongTien),
                ChiTietHoaDonDichVus = lines
            };
        }

        private static void MakeBilled(YeuCauSuCo incident)
        {
            incident.TrangThai = TrangThaiSuCo.DaHoanThanh;
            incident.ChiPhiSuaChua = 100000m;
            incident.CongVaoHoaDon = true;
            incident.NgayXuLy = new DateTime(2026, 9, 10, 10, 0, 0, DateTimeKind.Utc);
        }

        [Fact]
        public async Task SoftDelete_DeniedForStaffOfOtherBranch_IncidentUnchanged()
        {
            var (service, suCoStore, _, access, uow) = CreateService();
            var incident = SetupIncident(suCoStore, access, actorId: 2, branchId: 1);
            access.Permissions.Add((20, 2, EmployeeActionCodes.InvoiceDraft));

            var result = await service.SoftDeleteAsync(incident.Id, 20);

            Assert.False(result.Success);
            Assert.Contains("không có quyền", result.Message, StringComparison.OrdinalIgnoreCase);
            Assert.False(incident.IsDeleted);
            Assert.False(uow.SaveChangesCalled);
        }

        [Fact]
        public async Task SoftDelete_BilledIncident_WhenMonthInvoiceIsNhap_RemovesIncidentLineAndRecalculatesTotal()
        {
            var (service, suCoStore, issuanceStore, access, uow) = CreateService();
            var incident = SetupIncident(suCoStore, access);
            MakeBilled(incident);
            issuanceStore.ContractIds.Add(50);
            issuanceStore.BillableIncidents.Add(incident);
            var hoaDon = CreateSeptemberInvoice(TrangThaiPhatHanhHoaDon.Nhap, withIncidentLine: true);
            issuanceStore.Invoices[(incident.PhongTroId, incident.NguoiThueId, 9, 2026)] = hoaDon;

            var result = await service.SoftDeleteAsync(incident.Id, 1);

            Assert.True(result.Success);
            Assert.True(incident.IsDeleted);
            Assert.DoesNotContain(hoaDon.ChiTietHoaDonDichVus.Where(x => !x.IsDeleted), x => x.TenDichVu.StartsWith(InvoiceLineNames.SuCoPrefix));
            Assert.Equal(2000000m, hoaDon.TongTien);
            Assert.Contains(50, issuanceStore.LockedContractIds);
            Assert.True(uow.LastTx.Committed);
        }

        [Theory]
        [InlineData(TrangThaiPhatHanhHoaDon.ChoDuyet)]
        [InlineData(TrangThaiPhatHanhHoaDon.DaChot)]
        [InlineData(TrangThaiPhatHanhHoaDon.DaGui)]
        public async Task SoftDelete_BilledIncident_WhenMonthInvoiceIsNotNhap_ReturnsFail_NotDeleted(TrangThaiPhatHanhHoaDon invoiceStatus)
        {
            var (service, suCoStore, issuanceStore, access, uow) = CreateService();
            var incident = SetupIncident(suCoStore, access);
            MakeBilled(incident);
            issuanceStore.ContractIds.Add(50);
            issuanceStore.Invoices[(incident.PhongTroId, incident.NguoiThueId, 9, 2026)] = CreateSeptemberInvoice(invoiceStatus, withIncidentLine: true);

            var result = await service.SoftDeleteAsync(incident.Id, 1);

            Assert.False(result.Success);
            Assert.Equal("Hóa đơn tháng 9/2026 đã gửi duyệt hoặc đã chốt. Admin cần trả lại hoặc hủy hóa đơn trước khi sửa chi phí sự cố.", result.Message);
            Assert.False(incident.IsDeleted);
            Assert.True(uow.LastTx.RolledBack);
        }

        [Fact]
        public async Task SoftDelete_NonBilledIncident_Succeeds()
        {
            var (service, suCoStore, issuanceStore, access, uow) = CreateService();
            var incident = SetupIncident(suCoStore, access);

            var result = await service.SoftDeleteAsync(incident.Id, 1);

            Assert.True(result.Success);
            Assert.True(incident.IsDeleted);
            Assert.True(uow.SaveChangesCalled);
            Assert.Empty(issuanceStore.LockedContractIds);
        }

        [Fact]
        public async Task SoftDelete_BilledIncident_NoInvoiceYet_Succeeds()
        {
            var (service, suCoStore, _, access, _) = CreateService();
            var incident = SetupIncident(suCoStore, access);
            MakeBilled(incident);

            var result = await service.SoftDeleteAsync(incident.Id, 1);

            Assert.True(result.Success);
            Assert.True(incident.IsDeleted);
        }

        [Fact]
        public async Task SoftDelete_BilledIncident_DbError_ReturnsFixedMessage_AndRestoresState()
        {
            var (service, suCoStore, issuanceStore, access, uow) = CreateService();
            var incident = SetupIncident(suCoStore, access);
            MakeBilled(incident);
            issuanceStore.ContractIds.Add(50);
            issuanceStore.BillableIncidents.Add(incident);
            issuanceStore.Invoices[(incident.PhongTroId, incident.NguoiThueId, 9, 2026)] = CreateSeptemberInvoice(TrangThaiPhatHanhHoaDon.Nhap, withIncidentLine: true);
            uow.ThrowOnSave = true;

            var result = await service.SoftDeleteAsync(incident.Id, 1);

            Assert.False(result.Success);
            Assert.Equal("Không thể xóa sự cố. Vui lòng thử lại.", result.Message);
            Assert.DoesNotContain("SECRET_DB_ERROR_SUCO", result.Message);
            Assert.False(incident.IsDeleted);
            Assert.True(uow.LastTx.RolledBack);
        }
    }
}
