using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Security;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Services;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Common.Configurations;
using QuanLyChoThuePhongTroWeb.Application.Common.Security;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Services;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    public class InvoiceDraftWorkflowTests
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
            public bool ShouldThrowOnSave { get; set; }
            public Task<IApplicationTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
                => Task.FromResult<IApplicationTransaction>(new FakeApplicationTransaction());

            public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            {
                if (ShouldThrowOnSave)
                    throw new InvalidOperationException("DB Failure: deadlock detected");
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

        private class FakeCalculatorService : IHoaDonCalculatorService
        {
            public bool ShouldThrow { get; set; }
            public (decimal SoTien, string DienGiai, int SoNgayO) TinhTienPhong(decimal giaThue, DateTime batDau, DateTime? ketThuc, int thang, int nam)
            {
                if (ShouldThrow) throw new InvalidOperationException("Số ngày thuê không hợp lệ");
                return (giaThue, "Tiền phòng", 30);
            }

            public (decimal SoLuong, decimal SoTien, string DienGiai) TinhTienDienNuoc(decimal chiSoMoi, decimal chiSoCu, decimal donGia, string tenDichVu, int soNgayO, int tongNgayTrongThang)
            {
                var sl = chiSoMoi - chiSoCu;
                return (sl, sl * donGia, $"{tenDichVu}: {sl} x {donGia}");
            }

            public (decimal SoTien, string DienGiai) TinhTienDichVuCoDinh(decimal giaDv, decimal soLuong, string tenDichVu, DateTime batDau, DateTime? ketThuc, int thang, int nam, DateTime? contractStart = null, DateTime? contractEnd = null)
            {
                return (giaDv * soLuong, $"{tenDichVu}: {soLuong} x {giaDv}");
            }
        }

        private class FakeHoaDonStore : IHoaDonStore
        {
            public Dictionary<int, int> RoomBranches { get; set; } = new();
            public Dictionary<int, ChiNhanh> Branches { get; set; } = new();
            public List<HopDong> ValidContracts { get; set; } = new();
            public List<int> ExistingInvoiceContractIds { get; set; } = new();
            public List<DichVuDienNuocCuaPhong> MeterReadings { get; set; } = new();
            public List<DangKyDichVu> ServiceRegistrations { get; set; } = new();
            public List<PhongTro> Rooms { get; set; } = new();
            public List<YeuCauSuCo> BillableSuCos { get; set; } = new();

            public Task<IReadOnlyDictionary<int, int>> GetRoomBranchIdsAsync(IReadOnlyList<int> roomIds, CancellationToken cancellationToken = default)
            {
                var dict = roomIds.Where(id => RoomBranches.ContainsKey(id)).ToDictionary(id => id, id => RoomBranches[id]);
                return Task.FromResult<IReadOnlyDictionary<int, int>>(dict);
            }

            public Task<ChiNhanh?> GetChiNhanhByIdAsync(int chiNhanhId, CancellationToken cancellationToken = default)
            {
                Branches.TryGetValue(chiNhanhId, out var b);
                return Task.FromResult(b);
            }

            public Task<IReadOnlyList<HopDong>> GetValidContractsForBillingAsync(int chiNhanhId, IReadOnlyList<int> roomIds, DateTime startUtc, DateTime endUtc, CancellationToken cancellationToken = default)
            {
                var list = ValidContracts.Where(h => roomIds.Contains(h.PhongTroId)).ToList();
                return Task.FromResult<IReadOnlyList<HopDong>>(list);
            }

            public Task<IReadOnlyList<int>> GetExistingInvoiceContractIdsAsync(IReadOnlyList<int> contractIds, int thang, int nam, CancellationToken cancellationToken = default)
            {
                var list = ExistingInvoiceContractIds.Where(contractIds.Contains).ToList();
                return Task.FromResult<IReadOnlyList<int>>(list);
            }

            public Task<IReadOnlyList<DichVuDienNuocCuaPhong>> GetDichVuDienNuocByRoomIdsAsync(IReadOnlyList<int> roomIds, int thang, int nam, CancellationToken cancellationToken = default)
            {
                var list = MeterReadings.Where(m => roomIds.Contains(m.PhongTroId) && m.Thang == thang && m.Nam == nam).ToList();
                return Task.FromResult<IReadOnlyList<DichVuDienNuocCuaPhong>>(list);
            }

            public Task<IReadOnlyList<DangKyDichVu>> GetDangKyDichVusForBillingAsync(IReadOnlyList<int> roomIds, DateTime startUtc, DateTime endUtc, CancellationToken cancellationToken = default)
            {
                var list = ServiceRegistrations.Where(d => roomIds.Contains(d.PhongTroId)).ToList();
                return Task.FromResult<IReadOnlyList<DangKyDichVu>>(list);
            }

            public Task<IReadOnlyList<PhongTro>> GetPhongTrosByChiNhanhIdAsync(int chiNhanhId, CancellationToken cancellationToken = default)
                => Task.FromResult<IReadOnlyList<PhongTro>>(Rooms.Where(r => r.ChiNhanhId == chiNhanhId).ToList());

            public Task<IReadOnlyList<YeuCauSuCo>> GetBillableSuCosAsync(IReadOnlyList<int> roomIds, CancellationToken cancellationToken = default)
                => Task.FromResult<IReadOnlyList<YeuCauSuCo>>(BillableSuCos.Where(s => roomIds.Contains(s.PhongTroId)).ToList());

            public Task<IReadOnlyList<HoaDon>> GetInvoicesByMeterReadingIdsAsync(IReadOnlyList<int> meterReadingIds, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<HoaDon>>(new List<HoaDon>());
            public Task<HoaDon?> GetHoaDonWithDetailsForUpdateAsync(int id, CancellationToken cancellationToken = default) => Task.FromResult<HoaDon?>(null);
            public Task<DataTableResponse<HoaDonRes>> GetHoaDonsDataTableAsync(DataTableRequest request, int chiNhanhId, int thang, int nam, int trangThai, IReadOnlyList<int>? allowedBranchIds = null, CancellationToken cancellationToken = default) => Task.FromResult(new DataTableResponse<HoaDonRes>());
            public Task<HoaDonChiTietRes?> GetHoaDonDetailByIdAsync(int id, CancellationToken cancellationToken = default) => Task.FromResult<HoaDonChiTietRes?>(null);
            public Task<HoaDon?> GetActiveHoaDonByIdAsync(int id, CancellationToken cancellationToken = default) => Task.FromResult<HoaDon?>(null);
            public Task<IReadOnlyList<HoaDonRes>> GetUnpaidInvoicesAsync(int chiNhanhId, int thang, int nam, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<HoaDonRes>>(new List<HoaDonRes>());
            public Task<IReadOnlyList<int>> GetContractIdsByTenantIdAsync(int nguoiThueId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<int>>(new List<int>());
            public Task<IReadOnlyList<HoaDonRes>> GetInvoicesByContractIdsAsync(IReadOnlyList<int> contractIds, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<HoaDonRes>>(new List<HoaDonRes>());
            public Task<bool> CheckHoaDonOwnershipAsync(int hoaDonId, int nguoiThueId, CancellationToken cancellationToken = default) => Task.FromResult(false);
            public Task<IReadOnlyList<HoaDon>> GetOverdueInvoicesAsync(DateTime thresholdUtc, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<HoaDon>>(new List<HoaDon>());
            public Task<int?> GetHoaDonBranchIdAsync(int hoaDonId, CancellationToken cancellationToken = default) => Task.FromResult<int?>(null);
            public Task<InvoiceCancellationBlockers> GetCancellationBlockersAsync(int hoaDonId, CancellationToken cancellationToken = default) => Task.FromResult(new InvoiceCancellationBlockers(false, false, false));
            public Task AddInvoicesAsync(IEnumerable<HoaDon> invoices, CancellationToken cancellationToken = default) => Task.CompletedTask;
            public void UpdateSuCos(IEnumerable<YeuCauSuCo> suCos) { }
            public void RemoveChiTietHoaDons(IEnumerable<ChiTietHoaDon> chiTiets) { }
            public int UpdateHoaDonCalls { get; set; }
            public void UpdateHoaDon(HoaDon hoaDon) { UpdateHoaDonCalls++; }
            public Task AddLichSuThanhToanAsync(LichSuThanhToan lichSu, CancellationToken cancellationToken = default) => Task.CompletedTask;
        }

        private class FakeInvoiceIssuanceStore : IInvoiceIssuanceStore
        {
            public List<HoaDon> AddedInvoices { get; } = new();
            public Dictionary<int, int> CancelledCounts { get; } = new();
            public HashSet<int> ActiveInvoices { get; } = new();
            public List<YeuCauSuCo> Incidents { get; } = new();
            public int UpdateHoaDonCalls { get; private set; }

            public Task<IReadOnlyList<int>> LockContractsAsync(IReadOnlyList<int> hopDongIds, CancellationToken ct = default)
                => Task.FromResult<IReadOnlyList<int>>(hopDongIds.Distinct().OrderBy(x => x).ToList());

            public Task<HoaDon?> GetInvoiceForUpdateAsync(int hoaDonId, CancellationToken ct = default)
            {
                var inv = AddedInvoices.FirstOrDefault(x => x.HoaDonId == hoaDonId && !x.IsDeleted);
                return Task.FromResult(inv);
            }

            public Task<int> CountCancelledInvoicesAsync(int hopDongId, int thang, int nam, CancellationToken ct = default)
            {
                CancelledCounts.TryGetValue(hopDongId, out var count);
                return Task.FromResult(count);
            }

            public Task<bool> HasOtherActiveInvoiceAsync(int hopDongId, int thang, int nam, int excludeHoaDonId, CancellationToken ct = default)
            {
                return Task.FromResult(ActiveInvoices.Contains(hopDongId));
            }

            public Task AddInvoiceAsync(HoaDon hoaDon, CancellationToken ct = default)
            {
                hoaDon.HoaDonId = AddedInvoices.Count + 1;
                AddedInvoices.Add(hoaDon);
                return Task.CompletedTask;
            }

            public Task<IReadOnlyList<YeuCauSuCo>> GetBillableIncidentsInPeriodAsync(IReadOnlyList<int> roomIds, DateTime startUtc, DateTime nextMonthStartUtc, CancellationToken ct = default)
            {
                var list = Incidents
                    .Where(s => roomIds.Contains(s.PhongTroId) && !s.IsDeleted && s.CongVaoHoaDon && s.ChiPhiSuaChua > 0 && s.TrangThai == TrangThaiSuCo.DaHoanThanh && s.NgayXuLy.HasValue && s.NgayXuLy.Value >= startUtc && s.NgayXuLy.Value < nextMonthStartUtc)
                    .ToList();
                return Task.FromResult<IReadOnlyList<YeuCauSuCo>>(list);
            }

            public Task<HoaDon?> GetActiveInvoiceForTenantRoomPeriodForUpdateAsync(int phongTroId, int nguoiThueId, int thang, int nam, CancellationToken ct = default)
            {
                var inv = AddedInvoices.FirstOrDefault(h => h.HopDong?.PhongTroId == phongTroId && h.HopDong?.NguoiThueId == nguoiThueId && !h.IsDeleted);
                return Task.FromResult(inv);
            }

            public Task<(int? MeterPeriodId, int ChiNhanhId)?> GetInvoiceLockTargetsAsync(int hoaDonId, CancellationToken ct = default)
            {
                var inv = AddedInvoices.FirstOrDefault(h => h.HoaDonId == hoaDonId && !h.IsDeleted);
                if (inv == null) return Task.FromResult<(int? MeterPeriodId, int ChiNhanhId)?>(null);
                return Task.FromResult<(int? MeterPeriodId, int ChiNhanhId)?>((inv.DichVuDienNuocCuaPhongId, inv.HopDong?.PhongTro?.ChiNhanhId ?? 1));
            }

            public TrangThaiGhiNhan? PeriodStatus { get; set; } = TrangThaiGhiNhan.DaDuyet;

            public Task<TrangThaiGhiNhan?> LockMeterPeriodAsync(int meterPeriodId, CancellationToken ct = default)
            {
                return Task.FromResult<TrangThaiGhiNhan?>(PeriodStatus);
            }

            public Task<IReadOnlyList<int>> GetContractIdsForTenantRoomAsync(int phongTroId, int nguoiThueId, CancellationToken ct = default)
            {
                return Task.FromResult<IReadOnlyList<int>>(new List<int> { 1 });
            }
        }

        private (InvoiceIssuanceService service, FakeInvoiceIssuanceStore issuanceStore, FakeHoaDonStore hoaDonStore, FakeEmployeeAccessService accessService, FakeCalculatorService calcService, FakeUnitOfWork uow) CreateService()
        {
            var issuanceStore = new FakeInvoiceIssuanceStore();
            var hoaDonStore = new FakeHoaDonStore();
            var accessService = new FakeEmployeeAccessService();
            var calcService = new FakeCalculatorService();
            var uow = new FakeUnitOfWork();
            var logger = NullLogger<InvoiceIssuanceService>.Instance;

            var service = new InvoiceIssuanceService(
                issuanceStore,
                hoaDonStore,
                uow,
                accessService,
                calcService,
                logger);

            return (service, issuanceStore, hoaDonStore, accessService, calcService, uow);
        }

        private void SetupValidBranchAndRoom(FakeHoaDonStore store, FakeEmployeeAccessService access, int branchId = 1, int roomId = 101, int contractId = 10, int tenantId = 5, int actorId = 1)
        {
            store.Branches[branchId] = new ChiNhanh { ChiNhanhId = branchId, TenChiNhanh = "Chi nhánh 1", MaChiNhanh = "CN1" };
            store.RoomBranches[roomId] = branchId;
            var room = new PhongTro { PhongTroId = roomId, ChiNhanhId = branchId, SoPhong = "101" };
            var tenant = new NguoiThue { NguoiThueId = tenantId, HoVaTen = "Nguyen Van A" };
            var contract = new HopDong
            {
                HopDongId = contractId,
                PhongTroId = roomId,
                PhongTro = room,
                NguoiThueId = tenantId,
                NguoiThue = tenant,
                MaHopDong = "HD-01",
                TienThuePhong = 3000000m,
                ThoiDiemBatDau = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                ThoiDiemKetThuc = new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc),
                TrangThaiHopDong = TrangThaiHopDong.DangHoatDong
            };
            store.ValidContracts.Add(contract);
            store.Rooms.Add(room);

            // Kỳ chỉ số đã duyệt
            store.MeterReadings.Add(new DichVuDienNuocCuaPhong
            {
                DichVuDienNuocCuaPhongId = 201,
                PhongTroId = roomId,
                Thang = 9,
                Nam = 2026,
                TrangThaiGhiNhan = TrangThaiGhiNhan.DaDuyet,
                ChiSoDienCu = 100,
                ChiSoDienMoi = 150,
                DonGiaDien = 3500m,
                ChiSoNuocCu = 50,
                ChiSoNuocMoi = 60,
                DonGiaNuoc = 15000m
            });

            // Quyền InvoiceDraft
            access.Permissions.Add((actorId, branchId, EmployeeActionCodes.InvoiceDraft));
        }

        [Fact]
        public async Task CreateDrafts_Creates_Nhap_FromApprovedPeriod_WithHistoryRow()
        {
            var (service, issuanceStore, hoaDonStore, access, _, _) = CreateService();
            SetupValidBranchAndRoom(hoaDonStore, access);

            var req = new CreateInvoiceDraftsRequest
            {
                ChiNhanhId = 1,
                Thang = 9,
                Nam = 2026,
                PhongTroIds = new[] { 101 }
            };

            var result = await service.CreateDraftsAsync(req, actorId: 1);

            Assert.True(result.Success);
            Assert.Equal(1, result.Data!.CreatedCount);
            Assert.Single(issuanceStore.AddedInvoices);

            var invoice = issuanceStore.AddedInvoices.Single();
            Assert.Equal(TrangThaiPhatHanhHoaDon.Nhap, invoice.TrangThaiPhatHanh);
            Assert.Equal(201, invoice.DichVuDienNuocCuaPhongId);
            Assert.Single(invoice.LichSuTrangThaiHoaDons);
            var history = invoice.LichSuTrangThaiHoaDons.Single();
            Assert.Null(history.TrangThaiPhatHanhCu);
            Assert.Equal(TrangThaiPhatHanhHoaDon.Nhap, history.TrangThaiPhatHanhMoi);
            Assert.Equal(1, history.NguoiThucHienId);
            Assert.Equal("Tạo hóa đơn nháp", history.LyDo);

            // Tổng tiền khớp tổng chi tiết
            var sumDetails = invoice.ChiTietHoaDonDichVus.Where(x => !x.IsDeleted).Sum(x => x.TongTien);
            Assert.Equal(invoice.TongTien, sumDetails);
            Assert.True(invoice.TongTien > 0);
        }

        [Theory]
        [InlineData(TrangThaiGhiNhan.Nhap)]
        [InlineData(TrangThaiGhiNhan.ChoDuyet)]
        [InlineData(TrangThaiGhiNhan.TuChoi)]
        public async Task CreateDrafts_Skips_WhenPeriodNotDaDuyet(TrangThaiGhiNhan status)
        {
            var (service, issuanceStore, hoaDonStore, access, _, _) = CreateService();
            SetupValidBranchAndRoom(hoaDonStore, access);
            hoaDonStore.MeterReadings[0].TrangThaiGhiNhan = status;

            var req = new CreateInvoiceDraftsRequest { ChiNhanhId = 1, Thang = 9, Nam = 2026, PhongTroIds = new[] { 101 } };
            var result = await service.CreateDraftsAsync(req, actorId: 1);

            Assert.True(result.Success);
            Assert.Equal(0, result.Data!.CreatedCount);
            Assert.Equal(InvoiceDraftItemStatus.SkippedMeterNotApproved, result.Data.Items[0].Status);
            Assert.Empty(issuanceStore.AddedInvoices);
        }

        [Fact]
        public async Task CreateDrafts_Skips_WhenNoMeterReading()
        {
            var (service, issuanceStore, hoaDonStore, access, _, _) = CreateService();
            SetupValidBranchAndRoom(hoaDonStore, access);
            hoaDonStore.MeterReadings.Clear();

            var req = new CreateInvoiceDraftsRequest { ChiNhanhId = 1, Thang = 9, Nam = 2026, PhongTroIds = new[] { 101 } };
            var result = await service.CreateDraftsAsync(req, actorId: 1);

            Assert.True(result.Success);
            Assert.Equal(0, result.Data!.CreatedCount);
            Assert.Equal(InvoiceDraftItemStatus.SkippedNoMeterReading, result.Data.Items[0].Status);
            Assert.Empty(issuanceStore.AddedInvoices);
        }

        [Fact]
        public async Task CreateDrafts_Skips_WhenActiveInvoiceExists()
        {
            var (service, issuanceStore, hoaDonStore, access, _, _) = CreateService();
            SetupValidBranchAndRoom(hoaDonStore, access);
            hoaDonStore.ExistingInvoiceContractIds.Add(10); // Hợp đồng 10 đã có hóa đơn

            var req = new CreateInvoiceDraftsRequest { ChiNhanhId = 1, Thang = 9, Nam = 2026, PhongTroIds = new[] { 101 } };
            var result = await service.CreateDraftsAsync(req, actorId: 1);

            Assert.True(result.Success);
            Assert.Equal(0, result.Data!.CreatedCount);
            Assert.Equal(InvoiceDraftItemStatus.SkippedHasActiveInvoice, result.Data.Items[0].Status);
            Assert.Empty(issuanceStore.AddedInvoices);
        }

        [Fact]
        public async Task CreateDrafts_OneInvalidContract_OthersStillCreated()
        {
            var (service, issuanceStore, hoaDonStore, access, _, _) = CreateService();
            SetupValidBranchAndRoom(hoaDonStore, access, branchId: 1, roomId: 101, contractId: 10, tenantId: 5, actorId: 1);

            // Thêm phòng 102 hợp lệ
            hoaDonStore.RoomBranches[102] = 1;
            var room2 = new PhongTro { PhongTroId = 102, ChiNhanhId = 1, SoPhong = "102" };
            var contract2 = new HopDong
            {
                HopDongId = 11,
                PhongTroId = 102,
                PhongTro = room2,
                NguoiThueId = 6,
                NguoiThue = new NguoiThue { NguoiThueId = 6, HoVaTen = "Nguyen Van B" },
                MaHopDong = "HD-02",
                TienThuePhong = 2500000m,
                ThoiDiemBatDau = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                ThoiDiemKetThuc = new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc),
                TrangThaiHopDong = TrangThaiHopDong.DangHoatDong
            };
            hoaDonStore.ValidContracts.Add(contract2);
            hoaDonStore.MeterReadings.Add(new DichVuDienNuocCuaPhong
            {
                DichVuDienNuocCuaPhongId = 202,
                PhongTroId = 102,
                Thang = 9,
                Nam = 2026,
                TrangThaiGhiNhan = TrangThaiGhiNhan.DaDuyet,
                ChiSoDienCu = 50,
                ChiSoDienMoi = 80,
                DonGiaDien = 3500m,
                ChiSoNuocCu = 20,
                ChiSoNuocMoi = 30,
                DonGiaNuoc = 15000m
            });

            // Phòng 101 bị thiếu kỳ chỉ số
            hoaDonStore.MeterReadings.RemoveAll(x => x.PhongTroId == 101);

            var req = new CreateInvoiceDraftsRequest { ChiNhanhId = 1, Thang = 9, Nam = 2026, PhongTroIds = new[] { 101, 102 } };
            var result = await service.CreateDraftsAsync(req, actorId: 1);

            Assert.True(result.Success);
            Assert.Equal(1, result.Data!.CreatedCount);
            Assert.Equal(InvoiceDraftItemStatus.SkippedNoMeterReading, result.Data.Items.First(x => x.PhongTroId == 101).Status);
            Assert.Equal(InvoiceDraftItemStatus.Created, result.Data.Items.First(x => x.PhongTroId == 102).Status);
            Assert.Single(issuanceStore.AddedInvoices);
        }

        [Fact]
        public async Task CreateDrafts_Rejects_StaffOfOtherBranch_And_BranchMismatch()
        {
            var (service, _, hoaDonStore, access, _, _) = CreateService();
            SetupValidBranchAndRoom(hoaDonStore, access, actorId: 1);

            // 1. Nhân viên không có quyền tại chi nhánh 1
            var req = new CreateInvoiceDraftsRequest { ChiNhanhId = 1, Thang = 9, Nam = 2026, PhongTroIds = new[] { 101 } };
            var resultOtherStaff = await service.CreateDraftsAsync(req, actorId: 99);
            Assert.False(resultOtherStaff.Success);
            Assert.Contains("quyền", resultOtherStaff.Message, StringComparison.OrdinalIgnoreCase);

            // 2. Chi nhánh request khác chi nhánh thực của phòng
            var reqMismatch = new CreateInvoiceDraftsRequest { ChiNhanhId = 2, Thang = 9, Nam = 2026, PhongTroIds = new[] { 101 } };
            var resultMismatch = await service.CreateDraftsAsync(reqMismatch, actorId: 1);
            Assert.False(resultMismatch.Success);
            Assert.Contains("khớp", resultMismatch.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task CreateDrafts_AllowsAssignedStaff_And_Admin()
        {
            var (service, _, hoaDonStore, access, _, _) = CreateService();
            SetupValidBranchAndRoom(hoaDonStore, access, actorId: 20);

            // Nhân viên 20 được gán quyền tại CN1
            var req = new CreateInvoiceDraftsRequest { ChiNhanhId = 1, Thang = 9, Nam = 2026, PhongTroIds = new[] { 101 } };
            var resultStaff = await service.CreateDraftsAsync(req, actorId: 20);
            Assert.True(resultStaff.Success);

            // Admin (actorId 1) cũng được gán quyền
            access.Permissions.Add((1, 1, EmployeeActionCodes.InvoiceDraft));
            hoaDonStore.ExistingInvoiceContractIds.Clear();
            var resultAdmin = await service.CreateDraftsAsync(req, actorId: 1);
            Assert.True(resultAdmin.Success);
        }

        [Fact]
        public async Task CreateDrafts_ReplacementAfterCancel_UsesSuffixR1ThenR2()
        {
            var (service, issuanceStore, hoaDonStore, access, _, _) = CreateService();
            SetupValidBranchAndRoom(hoaDonStore, access);

            var req = new CreateInvoiceDraftsRequest { ChiNhanhId = 1, Thang = 9, Nam = 2026, PhongTroIds = new[] { 101 } };

            // Chưa có bản hủy nào -> mã gốc
            issuanceStore.CancelledCounts[10] = 0;
            var res1 = await service.CreateDraftsAsync(req, actorId: 1);
            Assert.True(res1.Success);
            Assert.Equal("HD-CN1-P101-10-092026", issuanceStore.AddedInvoices[0].MaHoaDon);

            // Đã có 1 bản hủy -> hậu tố -R1
            issuanceStore.CancelledCounts[10] = 1;
            issuanceStore.AddedInvoices.Clear();
            var res2 = await service.CreateDraftsAsync(req, actorId: 1);
            Assert.True(res2.Success);
            Assert.Equal("HD-CN1-P101-10-092026-R1", issuanceStore.AddedInvoices[0].MaHoaDon);

            // Đã có 2 bản hủy -> hậu tố -R2
            issuanceStore.CancelledCounts[10] = 2;
            issuanceStore.AddedInvoices.Clear();
            var res3 = await service.CreateDraftsAsync(req, actorId: 1);
            Assert.True(res3.Success);
            Assert.Equal("HD-CN1-P101-10-092026-R2", issuanceStore.AddedInvoices[0].MaHoaDon);
        }

        [Fact]
        public async Task CreateDrafts_IncludesOnlyIncidentsProcessedInThatMonth_ForThatTenant()
        {
            var (service, issuanceStore, hoaDonStore, access, _, _) = CreateService();
            SetupValidBranchAndRoom(hoaDonStore, access, tenantId: 5);

            // Sự cố tháng 9 của khách 5 (200.000)
            issuanceStore.Incidents.Add(new YeuCauSuCo
            {
                PhongTroId = 101,
                NguoiThueId = 5,
                TieuDe = "Sửa khóa",
                TrangThai = TrangThaiSuCo.DaHoanThanh,
                CongVaoHoaDon = true,
                ChiPhiSuaChua = 200000m,
                NgayXuLy = new DateTime(2026, 9, 15, 10, 0, 0, DateTimeKind.Utc)
            });

            // Sự cố tháng 9 nhưng của khách khác (cùng phòng) -> Không được tính
            issuanceStore.Incidents.Add(new YeuCauSuCo
            {
                PhongTroId = 101,
                NguoiThueId = 999,
                TieuDe = "Sửa ống nước khách cũ",
                TrangThai = TrangThaiSuCo.DaHoanThanh,
                CongVaoHoaDon = true,
                ChiPhiSuaChua = 300000m,
                NgayXuLy = new DateTime(2026, 9, 10, 10, 0, 0, DateTimeKind.Utc)
            });

            // Sự cố tháng 8 -> Không được tính
            issuanceStore.Incidents.Add(new YeuCauSuCo
            {
                PhongTroId = 101,
                NguoiThueId = 5,
                TieuDe = "Sửa đèn tháng 8",
                TrangThai = TrangThaiSuCo.DaHoanThanh,
                CongVaoHoaDon = true,
                ChiPhiSuaChua = 150000m,
                NgayXuLy = new DateTime(2026, 8, 20, 10, 0, 0, DateTimeKind.Utc)
            });

            var req = new CreateInvoiceDraftsRequest { ChiNhanhId = 1, Thang = 9, Nam = 2026, PhongTroIds = new[] { 101 } };
            var result = await service.CreateDraftsAsync(req, actorId: 1);

            Assert.True(result.Success);
            var inv = issuanceStore.AddedInvoices.Single();
            var incidentLines = inv.ChiTietHoaDonDichVus.Where(x => x.TenDichVu.StartsWith(InvoiceLineNames.SuCoPrefix)).ToList();
            Assert.Single(incidentLines);
            Assert.Equal("Sửa chữa sự cố: Sửa khóa", incidentLines[0].TenDichVu);
            Assert.Equal(200000m, incidentLines[0].TongTien);
        }

        [Fact]
        public async Task CreateDrafts_DoesNotTurnOffCongVaoHoaDon()
        {
            var (service, issuanceStore, hoaDonStore, access, _, _) = CreateService();
            SetupValidBranchAndRoom(hoaDonStore, access, tenantId: 5);

            var incident = new YeuCauSuCo
            {
                PhongTroId = 101,
                NguoiThueId = 5,
                TieuDe = "Sửa vòi nước",
                TrangThai = TrangThaiSuCo.DaHoanThanh,
                CongVaoHoaDon = true,
                ChiPhiSuaChua = 150000m,
                NgayXuLy = new DateTime(2026, 9, 5, 10, 0, 0, DateTimeKind.Utc)
            };
            issuanceStore.Incidents.Add(incident);

            var req = new CreateInvoiceDraftsRequest { ChiNhanhId = 1, Thang = 9, Nam = 2026, PhongTroIds = new[] { 101 } };
            var result = await service.CreateDraftsAsync(req, actorId: 1);

            Assert.True(result.Success);
            // D1: CongVaoHoaDon KHÔNG BAO GIỜ bị tắt khi tạo hay hủy hóa đơn
            Assert.True(incident.CongVaoHoaDon);
        }

        [Fact]
        public async Task CreateDrafts_ReplacementAfterCancel_IncludesSameIncidentsAgain()
        {
            var (service, issuanceStore, hoaDonStore, access, _, _) = CreateService();
            SetupValidBranchAndRoom(hoaDonStore, access, tenantId: 5);

            issuanceStore.Incidents.Add(new YeuCauSuCo
            {
                PhongTroId = 101,
                NguoiThueId = 5,
                TieuDe = "Sửa bóng đèn",
                TrangThai = TrangThaiSuCo.DaHoanThanh,
                CongVaoHoaDon = true,
                ChiPhiSuaChua = 200000m,
                NgayXuLy = new DateTime(2026, 9, 12, 10, 0, 0, DateTimeKind.Utc)
            });

            // Lần 1: tạo nháp bản gốc
            var req = new CreateInvoiceDraftsRequest { ChiNhanhId = 1, Thang = 9, Nam = 2026, PhongTroIds = new[] { 101 } };
            await service.CreateDraftsAsync(req, actorId: 1);

            // Giả lập hủy và tạo bản thay thế -R1
            issuanceStore.AddedInvoices.Clear();
            issuanceStore.CancelledCounts[10] = 1;

            var res2 = await service.CreateDraftsAsync(req, actorId: 1);
            Assert.True(res2.Success);
            var invReplacement = issuanceStore.AddedInvoices.Single();
            var incidentLines = invReplacement.ChiTietHoaDonDichVus.Where(x => x.TenDichVu.StartsWith(InvoiceLineNames.SuCoPrefix)).ToList();
            Assert.Single(incidentLines);
            Assert.Equal(200000m, incidentLines[0].TongTien);
        }

        [Fact]
        public async Task CreateDrafts_Replacement_RecomputesRoomAndMeterLines()
        {
            var (service, issuanceStore, hoaDonStore, access, _, _) = CreateService();
            SetupValidBranchAndRoom(hoaDonStore, access);

            // Tạo lần 1 với điện mới = 150 (dùng 50 số x 3500 = 175.000)
            var req = new CreateInvoiceDraftsRequest { ChiNhanhId = 1, Thang = 9, Nam = 2026, PhongTroIds = new[] { 101 } };
            await service.CreateDraftsAsync(req, actorId: 1);

            // Sửa số điện mới thành 180 (dùng 80 số x 3500 = 280.000) trước khi tạo bản thay thế
            hoaDonStore.MeterReadings[0].ChiSoDienMoi = 180;
            issuanceStore.AddedInvoices.Clear();
            issuanceStore.CancelledCounts[10] = 1;

            var res2 = await service.CreateDraftsAsync(req, actorId: 1);
            Assert.True(res2.Success);
            var invReplacement = issuanceStore.AddedInvoices.Single();
            var dienLine = invReplacement.ChiTietHoaDonDichVus.First(x => x.TenDichVu.StartsWith("Điện"));
            Assert.Equal(80m, dienLine.SoLuong);
            Assert.Equal(280000m, dienLine.TongTien);
        }

        [Theory]
        [InlineData("Điện", "kWh", 3500)]
        [InlineData("Nước", "m3", 15000)]
        [InlineData("Tiền điện", "", 3500)]
        [InlineData("Điện sinh hoạt", "kWh", 3500)]
        public async Task CreateDrafts_DoesNotBillMeterServiceTwice_WhenRegisteredAsFixedService(string tenDichVu, string donVi, int gia)
        {
            var (service, issuanceStore, hoaDonStore, access, _, _) = CreateService();
            SetupValidBranchAndRoom(hoaDonStore, access);

            // Dịch vụ điện/nước tạo tay trên giao diện luôn có LoaiDichVu = Khac
            hoaDonStore.ServiceRegistrations.Add(NewRegistration(101, 900, tenDichVu, donVi, gia, LoaiDichVu.Khac));
            hoaDonStore.ServiceRegistrations.Add(NewRegistration(101, 901, "Đổ rác", "tháng", 50000, LoaiDichVu.Khac));

            var req = new CreateInvoiceDraftsRequest { ChiNhanhId = 1, Thang = 9, Nam = 2026, PhongTroIds = new[] { 101 } };
            var result = await service.CreateDraftsAsync(req, actorId: 1);

            Assert.True(result.Success);
            var lines = issuanceStore.AddedInvoices.Single().ChiTietHoaDonDichVus;

            // Phòng + điện theo chỉ số + nước theo chỉ số + đổ rác, không có dòng dịch vụ điện/nước cố định
            Assert.Equal(4, lines.Count);
            Assert.Single(lines, x => x.TenDichVu.StartsWith("Điện"));
            Assert.Single(lines, x => x.TenDichVu.StartsWith("Nước"));
            Assert.DoesNotContain(lines, x => x.DichVuId == 900);
            Assert.Contains(lines, x => x.DichVuId == 901);
            Assert.Equal(3000000m + 175000m + 150000m + 50000m, issuanceStore.AddedInvoices.Single().TongTien);
        }

        private static DangKyDichVu NewRegistration(int roomId, int dichVuId, string ten, string donVi, decimal gia, LoaiDichVu loai)
        {
            var dichVu = new DichVu { DichVuId = dichVuId, TenDichVu = ten, DonVi = donVi, LoaiDichVu = loai };
            return new DangKyDichVu
            {
                PhongTroId = roomId,
                SoLuong = 1,
                NgayBatDau = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                DichVuChiNhanh = new DichVuChiNhanh { DichVuId = dichVuId, DichVu = dichVu, GiaDichVu = gia }
            };
        }

        [Fact]
        public async Task CreateDrafts_DbFailure_ReturnsSafeMessage_NoRawException()
        {
            var (service, _, hoaDonStore, access, _, uow) = CreateService();
            SetupValidBranchAndRoom(hoaDonStore, access);
            uow.ShouldThrowOnSave = true;

            var req = new CreateInvoiceDraftsRequest { ChiNhanhId = 1, Thang = 9, Nam = 2026, PhongTroIds = new[] { 101 } };
            var result = await service.CreateDraftsAsync(req, actorId: 1);

            Assert.False(result.Success);
            Assert.Equal("Không thể tạo hóa đơn nháp. Vui lòng thử lại.", result.Message);
            Assert.DoesNotContain("deadlock", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        private HoaDon CreateSampleInvoice(int id = 1, TrangThaiPhatHanhHoaDon status = TrangThaiPhatHanhHoaDon.Nhap, decimal total = 2000000m, int? meterPeriodId = 10)
        {
            var inv = new HoaDon
            {
                HoaDonId = id,
                HopDongId = 10,
                MaHoaDon = $"HD-CN1-P101-10-092026",
                Thang = 9,
                Nam = 2026,
                TrangThaiPhatHanh = status,
                DichVuDienNuocCuaPhongId = meterPeriodId,
                TongTien = total,
                HopDong = new HopDong
                {
                    HopDongId = 10,
                    PhongTro = new PhongTro
                    {
                        PhongTroId = 101,
                        ChiNhanhId = 1
                    }
                },
                ChiTietHoaDonDichVus = new List<ChiTietHoaDon>
                {
                    new ChiTietHoaDon { ChiTietHoaDonId = 1, TenDichVu = "Tiền phòng", DonGia = total, SoLuong = 1, TongTien = total, DichVuId = 1 }
                }
            };
            return inv;
        }

        [Fact]
        public async Task Submit_AssignedStaff_MovesNhapToChoDuyet_AddsHistory()
        {
            var (service, issuanceStore, _, accessService, _, _) = CreateService();
            var inv = CreateSampleInvoice();
            issuanceStore.AddedInvoices.Add(inv);
            accessService.Permissions.Add((10, 1, EmployeeActionCodes.InvoiceSubmit));

            var result = await service.SubmitAsync(inv.HoaDonId, actorId: 10);

            Assert.True(result.Success);
            Assert.Equal(TrangThaiPhatHanhHoaDon.ChoDuyet, result.Data!.TrangThaiPhatHanh);
            Assert.Equal(TrangThaiPhatHanhHoaDon.ChoDuyet, inv.TrangThaiPhatHanh);
            Assert.Contains(inv.LichSuTrangThaiHoaDons, h => h.TrangThaiPhatHanhMoi == TrangThaiPhatHanhHoaDon.ChoDuyet && h.NguoiThucHienId == 10);
        }

        [Fact]
        public async Task Submit_Fails_WhenNotNhap_OrEmptyDetails_OrTotalMismatch()
        {
            var (service, issuanceStore, _, accessService, _, _) = CreateService();
            accessService.Permissions.Add((10, 1, EmployeeActionCodes.InvoiceSubmit));

            // Case 1: Not Nhap
            var inv1 = CreateSampleInvoice(1, TrangThaiPhatHanhHoaDon.ChoDuyet);
            issuanceStore.AddedInvoices.Add(inv1);
            var res1 = await service.SubmitAsync(1, 10);
            Assert.False(res1.Success);
            Assert.Contains("trạng thái Nháp", res1.Message);

            // Case 2: Empty details
            var inv2 = CreateSampleInvoice(2, TrangThaiPhatHanhHoaDon.Nhap);
            inv2.ChiTietHoaDonDichVus.Clear();
            issuanceStore.AddedInvoices.Add(inv2);
            var res2 = await service.SubmitAsync(2, 10);
            Assert.False(res2.Success);
            Assert.Contains("chi tiết dịch vụ", res2.Message);

            // Case 3: Total mismatch
            var inv3 = CreateSampleInvoice(3, TrangThaiPhatHanhHoaDon.Nhap, total: 3000000m);
            // Details sum is 2,000,000 but TongTien is 3,000,000
            inv3.ChiTietHoaDonDichVus.First().TongTien = 2000000m;
            issuanceStore.AddedInvoices.Add(inv3);
            var res3 = await service.SubmitAsync(3, 10);
            Assert.False(res3.Success);
            Assert.Contains("không khớp", res3.Message);
        }

        [Fact]
        public async Task Finalize_Admin_MovesChoDuyetToDaChot_SetsNguoiChotAndNgayChot()
        {
            var (service, issuanceStore, _, accessService, _, _) = CreateService();
            var inv = CreateSampleInvoice(1, TrangThaiPhatHanhHoaDon.ChoDuyet);
            issuanceStore.AddedInvoices.Add(inv);
            accessService.Permissions.Add((1, 1, EmployeeActionCodes.InvoiceFinalize));

            var result = await service.FinalizeAsync(inv.HoaDonId, actorId: 1);

            Assert.True(result.Success);
            Assert.Equal(TrangThaiPhatHanhHoaDon.DaChot, result.Data!.TrangThaiPhatHanh);
            Assert.Equal(TrangThaiPhatHanhHoaDon.DaChot, inv.TrangThaiPhatHanh);
            Assert.Equal(1, inv.NguoiChotId);
            Assert.NotNull(inv.NgayChot);
            Assert.Contains(inv.LichSuTrangThaiHoaDons, h => h.TrangThaiPhatHanhMoi == TrangThaiPhatHanhHoaDon.DaChot && h.NguoiThucHienId == 1);
        }

        [Fact]
        public async Task Finalize_And_Reject_DeniedForStaff_EvenWhenAssigned()
        {
            var (service, issuanceStore, _, accessService, _, _) = CreateService();
            var inv = CreateSampleInvoice(1, TrangThaiPhatHanhHoaDon.ChoDuyet);
            issuanceStore.AddedInvoices.Add(inv);
            // Staff 10 does not have InvoiceFinalize or InvoiceReject
            accessService.Permissions.Add((10, 1, EmployeeActionCodes.InvoiceSubmit));

            var finRes = await service.FinalizeAsync(inv.HoaDonId, actorId: 10);
            Assert.False(finRes.Success);
            Assert.Contains("Quản trị viên", finRes.Message);

            var rejRes = await service.RejectAsync(inv.HoaDonId, "Lý do", actorId: 10);
            Assert.False(rejRes.Success);
            Assert.Contains("Quản trị viên", rejRes.Message);
        }

        [Fact]
        public async Task Finalize_Fails_WhenLinkedPeriodNoLongerDaDuyet()
        {
            var (service, issuanceStore, _, accessService, _, _) = CreateService();
            var inv = CreateSampleInvoice(1, TrangThaiPhatHanhHoaDon.ChoDuyet, meterPeriodId: 5);
            issuanceStore.AddedInvoices.Add(inv);
            accessService.Permissions.Add((1, 1, EmployeeActionCodes.InvoiceFinalize));

            // Kỳ chỉ số liên kết bị chuyển về Chờ duyệt hoặc Từ chối
            issuanceStore.PeriodStatus = TrangThaiGhiNhan.ChoDuyet;

            var result = await service.FinalizeAsync(inv.HoaDonId, actorId: 1);

            Assert.False(result.Success);
            Assert.Contains("không còn ở trạng thái đã duyệt", result.Message);
            Assert.Equal(TrangThaiPhatHanhHoaDon.ChoDuyet, inv.TrangThaiPhatHanh);
        }

        [Fact]
        public async Task Finalize_Fails_WhenTotalDoesNotMatchDetails()
        {
            var (service, issuanceStore, _, accessService, _, _) = CreateService();
            var inv = CreateSampleInvoice(1, TrangThaiPhatHanhHoaDon.ChoDuyet, total: 3000000m);
            inv.ChiTietHoaDonDichVus.First().TongTien = 2000000m;
            issuanceStore.AddedInvoices.Add(inv);
            accessService.Permissions.Add((1, 1, EmployeeActionCodes.InvoiceFinalize));

            var result = await service.FinalizeAsync(inv.HoaDonId, actorId: 1);

            Assert.False(result.Success);
            Assert.Contains("không khớp", result.Message);
            Assert.Equal(TrangThaiPhatHanhHoaDon.ChoDuyet, inv.TrangThaiPhatHanh);
        }

        [Fact]
        public async Task Reject_RequiresReason_MovesBackToNhap_ThenUpdateHoaDonAllowed()
        {
            var (service, issuanceStore, _, accessService, _, _) = CreateService();
            var inv = CreateSampleInvoice(1, TrangThaiPhatHanhHoaDon.ChoDuyet);
            issuanceStore.AddedInvoices.Add(inv);
            accessService.Permissions.Add((1, 1, EmployeeActionCodes.InvoiceReject));

            // Bắt buộc lý do
            var resEmpty = await service.RejectAsync(inv.HoaDonId, "   ", actorId: 1);
            Assert.False(resEmpty.Success);
            Assert.Contains("bắt buộc", resEmpty.Message);

            // Trả lại hợp lệ
            var resValid = await service.RejectAsync(inv.HoaDonId, "Chỉ số nước sai thực tế", actorId: 1);
            Assert.True(resValid.Success);
            Assert.Equal(TrangThaiPhatHanhHoaDon.Nhap, inv.TrangThaiPhatHanh);
            var history = inv.LichSuTrangThaiHoaDons.Last();
            Assert.Equal(TrangThaiPhatHanhHoaDon.Nhap, history.TrangThaiPhatHanhMoi);
            Assert.Equal("Chỉ số nước sai thực tế", history.LyDo);
        }

        [Fact]
        public async Task Transitions_OnCancelledOrMissingInvoice_ReturnNotFoundFail_NoException()
        {
            var (service, issuanceStore, _, accessService, _, _) = CreateService();
            accessService.Permissions.Add((1, 1, EmployeeActionCodes.InvoiceSubmit));
            accessService.Permissions.Add((1, 1, EmployeeActionCodes.InvoiceFinalize));
            accessService.Permissions.Add((1, 1, EmployeeActionCodes.InvoiceReject));

            // Missing invoice
            var r1 = await service.SubmitAsync(9999, actorId: 1);
            Assert.False(r1.Success);
            Assert.Contains("Không tìm thấy", r1.Message);

            // Deleted invoice
            var inv = CreateSampleInvoice(5, TrangThaiPhatHanhHoaDon.Nhap);
            inv.IsDeleted = true;
            issuanceStore.AddedInvoices.Add(inv);

            var r2 = await service.SubmitAsync(5, actorId: 1);
            Assert.False(r2.Success);
            Assert.Contains("Không tìm thấy", r2.Message);

            var r3 = await service.FinalizeAsync(5, actorId: 1);
            Assert.False(r3.Success);
            Assert.Contains("Không tìm thấy", r3.Message);

            var r4 = await service.RejectAsync(5, "Lý do", actorId: 1);
            Assert.False(r4.Success);
            Assert.Contains("Không tìm thấy", r4.Message);
        }

        [Fact]
        public async Task Transitions_DoNotCallUpdateHoaDon()
        {
            var (service, issuanceStore, hoaDonStore, accessService, _, _) = CreateService();
            var inv = CreateSampleInvoice(1, TrangThaiPhatHanhHoaDon.Nhap);
            issuanceStore.AddedInvoices.Add(inv);
            accessService.Permissions.Add((1, 1, EmployeeActionCodes.InvoiceSubmit));
            accessService.Permissions.Add((1, 1, EmployeeActionCodes.InvoiceFinalize));
            accessService.Permissions.Add((1, 1, EmployeeActionCodes.InvoiceReject));

            await service.SubmitAsync(1, actorId: 1);
            await service.FinalizeAsync(1, actorId: 1);

            Assert.Equal(0, hoaDonStore.UpdateHoaDonCalls);
        }

        private class FakeDocumentExporter : IInvoiceDocumentExporter
        {
            public byte[] ExportExcel(HoaDonChiTietRes hoaDon) => new byte[] { 1 };
            public byte[] ExportPdf(HoaDonChiTietRes hoaDon, byte[]? qrBytes, string bankId, string accountNumber, string accountName) => new byte[] { 2 };
        }

        private class FakeVietQRService : IVietQRService
        {
            public string GenerateVietQRString(string bankId, string accountNumber, decimal amount, string memo) => "QR";
            public byte[] GenerateQRCodePNGBytes(string qrString) => new byte[] { 3 };
        }

        private class FakeEmailService : IEmailService
        {
            public Task<(bool IsSuccess, string ErrorMessage)> SendInvoiceEmailAsync(string toEmail, HoaDonChiTietRes hoaDon, byte[] pdfBytes) => Task.FromResult((true, string.Empty));
            public Task<(bool IsSuccess, string ErrorMessage)> SendContractExpiryAlertAsync(string toEmail, string tenNguoiNhan, QuanLyChoThuePhongTroWeb.Application.Features.Emails.DTOs.ContractExpiryAlertData alertData) => Task.FromResult((true, string.Empty));
        }

        [Fact]
        public async Task Preview_MarksNotReady_WhenPeriodNotDaDuyet()
        {
            var fakeHoaDonStore = new FakeHoaDonStore();
            var fakeIssuanceStore = new FakeInvoiceIssuanceStore();
            var fakeAccessService = new FakeEmployeeAccessService();
            fakeAccessService.Permissions.Add((1, 1, EmployeeActionCodes.InvoiceRead));
            fakeAccessService.Permissions.Add((1, 1, EmployeeActionCodes.InvoiceDraft));

            var room = new PhongTro { PhongTroId = 10, ChiNhanhId = 1, SoPhong = "P101", GiaThue = 2000000m };
            fakeHoaDonStore.Rooms.Add(room);

            var contract = new HopDong
            {
                HopDongId = 100,
                PhongTroId = 10,
                NguoiThueId = 20,
                TienThuePhong = 2000000m,
                ThoiDiemBatDau = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                TrangThaiHopDong = TrangThaiHopDong.DangHoatDong,
                NguoiThue = new NguoiThue { NguoiThueId = 20, HoVaTen = "Nguyễn Văn A" }
            };
            fakeHoaDonStore.ValidContracts.Add(contract);

            // Kỳ chỉ số chưa duyệt (ChoDuyet)
            fakeHoaDonStore.MeterReadings.Add(new DichVuDienNuocCuaPhong
            {
                DichVuDienNuocCuaPhongId = 50,
                PhongTroId = 10,
                Thang = 9,
                Nam = 2026,
                ChiSoDienCu = 100,
                ChiSoDienMoi = 150,
                DonGiaDien = 3500,
                TrangThaiGhiNhan = TrangThaiGhiNhan.ChoDuyet
            });

            var hoaDonService = new HoaDonService(
                fakeHoaDonStore,
                new FakeUnitOfWork(),
                new VietQrSettings(),
                NullLogger<HoaDonService>.Instance,
                new FakeCalculatorService(),
                new FakeDocumentExporter(),
                new FakeVietQRService(),
                fakeAccessService,
                new FakeEmailService(),
                fakeIssuanceStore);

            var previews = await hoaDonService.PreviewPhatSinhHoaDonAsync(chiNhanhId: 1, thang: 9, nam: 2026, actorId: 1);
            var item = Assert.Single(previews);
            Assert.False(item.DaChotDienNuoc);
            Assert.Equal("Chỉ số điện/nước chưa được duyệt", item.GhiChuTrangThai);
        }

        [Fact]
        public async Task Preview_IncidentTotal_MatchesDraftCreationRule()
        {
            var fakeHoaDonStore = new FakeHoaDonStore();
            var fakeIssuanceStore = new FakeInvoiceIssuanceStore();
            var fakeAccessService = new FakeEmployeeAccessService();
            fakeAccessService.Permissions.Add((1, 1, EmployeeActionCodes.InvoiceRead));
            fakeAccessService.Permissions.Add((1, 1, EmployeeActionCodes.InvoiceDraft));

            var room = new PhongTro { PhongTroId = 10, ChiNhanhId = 1, SoPhong = "P101", GiaThue = 2000000m };
            fakeHoaDonStore.Rooms.Add(room);

            var contract = new HopDong
            {
                HopDongId = 100,
                PhongTroId = 10,
                NguoiThueId = 20,
                TienThuePhong = 2000000m,
                ThoiDiemBatDau = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                TrangThaiHopDong = TrangThaiHopDong.DangHoatDong,
                NguoiThue = new NguoiThue { NguoiThueId = 20, HoVaTen = "Nguyễn Văn A" }
            };
            fakeHoaDonStore.ValidContracts.Add(contract);

            fakeHoaDonStore.MeterReadings.Add(new DichVuDienNuocCuaPhong
            {
                DichVuDienNuocCuaPhongId = 50,
                PhongTroId = 10,
                Thang = 9,
                Nam = 2026,
                ChiSoDienCu = 100,
                ChiSoDienMoi = 150,
                DonGiaDien = 3500,
                TrangThaiGhiNhan = TrangThaiGhiNhan.DaDuyet
            });

            // Giả lập GetBillableIncidentsInPeriodAsync trả về sự cố hợp lệ theo D1
            fakeIssuanceStore.Incidents.Add(new YeuCauSuCo
            {
                Id = 1,
                PhongTroId = 10,
                NguoiThueId = 20,
                ChiPhiSuaChua = 200000m,
                CongVaoHoaDon = true,
                TrangThai = TrangThaiSuCo.DaHoanThanh,
                NgayXuLy = new DateTime(2026, 9, 15, 10, 0, 0, DateTimeKind.Utc)
            });

            var hoaDonService = new HoaDonService(
                fakeHoaDonStore,
                new FakeUnitOfWork(),
                new VietQrSettings(),
                NullLogger<HoaDonService>.Instance,
                new FakeCalculatorService(),
                new FakeDocumentExporter(),
                new FakeVietQRService(),
                fakeAccessService,
                new FakeEmailService(),
                fakeIssuanceStore);

            var previews = await hoaDonService.PreviewPhatSinhHoaDonAsync(chiNhanhId: 1, thang: 9, nam: 2026, actorId: 1);
            var item = Assert.Single(previews);
            Assert.True(item.DaChotDienNuoc);
            Assert.Equal(1, item.SuCoCount);
            // 2.000.000 (phòng) + 175.000 (điện: 50*3500) + 200.000 (sự cố) = 2.375.000
            Assert.Equal(2375000m, item.TongTienDuKien);
        }
    }
}
