using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Security;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Common.Security;
using QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.Services;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Services;
using QuanLyChoThuePhongTroWeb.Application.Features.NhanViens.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.NhanViens.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Features.NhanViens.Services;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    public class MeterReadingCorrectionTests
    {
        private class FakeApplicationTransaction : IApplicationTransaction
        {
            public bool Committed { get; private set; }
            public bool RolledBack { get; private set; }

            public Task CommitAsync(CancellationToken cancellationToken = default)
            {
                Committed = true;
                return Task.CompletedTask;
            }

            public Task RollbackAsync(CancellationToken cancellationToken = default)
            {
                RolledBack = true;
                return Task.CompletedTask;
            }

            public void Dispose() { }
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }

        private class FakeUnitOfWork : IUnitOfWork
        {
            public FakeApplicationTransaction CurrentTransaction { get; } = new();
            public int SaveChangesCalls { get; private set; }

            public Task<IApplicationTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult<IApplicationTransaction>(CurrentTransaction);
            }

            public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            {
                SaveChangesCalls++;
                return Task.FromResult(1);
            }
        }

        private class FakeDienNuocStore : IDienNuocStore
        {
            public Dictionary<int, int> RoomBranches { get; set; } = new();
            public Dictionary<int, string> RoomNames { get; set; } = new();
            public Dictionary<int, DichVuDienNuocCuaPhong> CurrentMonthRecords { get; set; } = new();
            public Dictionary<int, DichVuDienNuocCuaPhong> PreviousMonthRecords { get; set; } = new();
            public HashSet<int> LockedRooms { get; set; } = new();
            public List<DichVuDienNuocCuaPhong> UpdatedRecords { get; } = new();

            public Task<IReadOnlyList<HopDong>> GetActiveContractsInBranchAsync(int chiNhanhId, DateTime startOfMonth, DateTime endOfMonth, CancellationToken cancellationToken = default)
                => Task.FromResult<IReadOnlyList<HopDong>>(new List<HopDong>());

            public Task<IReadOnlyDictionary<int, DichVuDienNuocCuaPhong>> GetCurrentMonthRecordsAsync(IReadOnlyList<int> roomIds, int thang, int nam, CancellationToken cancellationToken = default)
                => Task.FromResult<IReadOnlyDictionary<int, DichVuDienNuocCuaPhong>>(CurrentMonthRecords);

            public Task<IReadOnlyDictionary<int, DichVuDienNuocCuaPhong>> GetPreviousMonthRecordsAsync(IReadOnlyList<int> roomIds, int prevThang, int prevNam, CancellationToken cancellationToken = default)
                => Task.FromResult<IReadOnlyDictionary<int, DichVuDienNuocCuaPhong>>(PreviousMonthRecords);

            public Task<ISet<int>> GetLockedRoomIdsAsync(IReadOnlyList<int> roomIds, int thang, int nam, CancellationToken cancellationToken = default)
                => Task.FromResult<ISet<int>>(LockedRooms);

            public Task<ISet<int>> GetRoomIdsWithContractInMonthAsync(IReadOnlyList<int> roomIds, int thang, int nam, CancellationToken cancellationToken = default)
                => Task.FromResult<ISet<int>>(new HashSet<int>());

            public Task<(decimal ChiSoDienMoi, decimal ChiSoNuocMoi)> GetNearestPreviousReadingAsync(int phongTroId, int thang, int nam, CancellationToken cancellationToken = default)
                => Task.FromResult((0m, 0m));

            public Task<decimal> GetServicePriceAsync(string serviceKeyword, int chiNhanhId, CancellationToken cancellationToken = default)
            {
                if (serviceKeyword.Contains("điện", StringComparison.OrdinalIgnoreCase)) return Task.FromResult(3500m);
                if (serviceKeyword.Contains("nước", StringComparison.OrdinalIgnoreCase)) return Task.FromResult(20000m);
                return Task.FromResult(1000m);
            }

            public Task<IReadOnlyDictionary<int, string>> GetRoomNumbersAsync(IReadOnlyList<int> roomIds, CancellationToken cancellationToken = default)
                => Task.FromResult<IReadOnlyDictionary<int, string>>(RoomNames);

            public Task<IReadOnlyDictionary<int, int>> GetRoomBranchIdsAsync(IReadOnlyList<int> roomIds, CancellationToken cancellationToken = default)
            {
                var dict = new Dictionary<int, int>();
                foreach (var id in roomIds)
                {
                    if (RoomBranches.TryGetValue(id, out var branchId))
                    {
                        dict[id] = branchId;
                    }
                }
                return Task.FromResult<IReadOnlyDictionary<int, int>>(dict);
            }

            public Task AddRecordAsync(DichVuDienNuocCuaPhong record, CancellationToken cancellationToken = default) => Task.CompletedTask;

            public void UpdateRecord(DichVuDienNuocCuaPhong record)
            {
                UpdatedRecords.Add(record);
            }
        }

        private class FakeHoaDonStore : IHoaDonStore
        {
            public List<HoaDon> Invoices { get; set; } = new();

            public Task<IReadOnlyList<HoaDon>> GetInvoicesByMeterReadingIdsAsync(IReadOnlyList<int> meterReadingIds, CancellationToken cancellationToken = default)
            {
                var list = Invoices.Where(h => h.DichVuDienNuocCuaPhongId.HasValue && meterReadingIds.Contains(h.DichVuDienNuocCuaPhongId.Value)).ToList();
                return Task.FromResult<IReadOnlyList<HoaDon>>(list);
            }

            public Task<ChiNhanh?> GetChiNhanhByIdAsync(int chiNhanhId, CancellationToken cancellationToken = default) => Task.FromResult<ChiNhanh?>(null);
            public Task<IReadOnlyList<HopDong>> GetValidContractsForBillingAsync(int chiNhanhId, IReadOnlyList<int> roomIds, DateTime startUtc, DateTime endUtc, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<HopDong>>(new List<HopDong>());
            public Task<IReadOnlyList<int>> GetExistingInvoiceContractIdsAsync(IReadOnlyList<int> contractIds, int thang, int nam, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<int>>(new List<int>());
            public Task<IReadOnlyList<DichVuDienNuocCuaPhong>> GetDichVuDienNuocByRoomIdsAsync(IReadOnlyList<int> roomIds, int thang, int nam, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<DichVuDienNuocCuaPhong>>(new List<DichVuDienNuocCuaPhong>());
            public Task<IReadOnlyList<DangKyDichVu>> GetDangKyDichVusForBillingAsync(IReadOnlyList<int> roomIds, DateTime startUtc, DateTime endUtc, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<DangKyDichVu>>(new List<DangKyDichVu>());
            public Task<IReadOnlyList<YeuCauSuCo>> GetBillableSuCosAsync(IReadOnlyList<int> roomIds, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<YeuCauSuCo>>(new List<YeuCauSuCo>());
            public Task<IReadOnlyList<PhongTro>> GetPhongTrosByChiNhanhIdAsync(int chiNhanhId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<PhongTro>>(new List<PhongTro>());
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
            public Task<IReadOnlyDictionary<int, int>> GetRoomBranchIdsAsync(IReadOnlyList<int> roomIds, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyDictionary<int, int>>(new Dictionary<int, int>());
            public Task<InvoiceCancellationBlockers> GetCancellationBlockersAsync(int hoaDonId, CancellationToken cancellationToken = default) => Task.FromResult(new InvoiceCancellationBlockers(false, false, false));
            public Task AddInvoicesAsync(IEnumerable<HoaDon> invoices, CancellationToken cancellationToken = default) => Task.CompletedTask;
            public void UpdateSuCos(IEnumerable<YeuCauSuCo> suCos) { }
            public void RemoveChiTietHoaDons(IEnumerable<ChiTietHoaDon> chiTiets) { }
            public void UpdateHoaDon(HoaDon hoaDon) { }
            public Task AddLichSuThanhToanAsync(LichSuThanhToan lichSu, CancellationToken cancellationToken = default) => Task.CompletedTask;
        }

        private class FakeEmployeeBranchStore : IEmployeeBranchStore
        {
            public Dictionary<int, EmployeeBranchActorDto> Actors { get; } = new();

            public Task<EmployeeBranchActorDto?> GetActorWithActiveBranchesAsync(int actorId, CancellationToken cancellationToken = default)
            {
                Actors.TryGetValue(actorId, out var actor);
                return Task.FromResult(actor);
            }

            public Task<bool> HasActiveBranchAssignmentAsync(int actorId, int chiNhanhId, CancellationToken cancellationToken = default)
            {
                if (Actors.TryGetValue(actorId, out var actor) && actor.IsActive)
                {
                    return Task.FromResult(actor.ActiveBranchIds != null && actor.ActiveBranchIds.Contains(chiNhanhId));
                }
                return Task.FromResult(false);
            }
        }

        private class FakeCalculatorService : IHoaDonCalculatorService
        {
            public (decimal SoTien, string DienGiai, int SoNgayO) TinhTienPhong(decimal giaThue, DateTime batDau, DateTime? ketThuc, int thang, int nam)
                => (giaThue, "Tiền thuê phòng", 30);

            public (decimal SoLuong, decimal SoTien, string DienGiai) TinhTienDienNuoc(decimal chiSoMoi, decimal chiSoCu, decimal donGia, string tenDichVu, int soNgayO, int tongNgayTrongThang)
            {
                var diff = chiSoMoi - chiSoCu;
                var amount = decimal.Round(diff * donGia, 2, MidpointRounding.AwayFromZero);
                return (diff, amount, $"{tenDichVu} ({chiSoCu} -> {chiSoMoi})");
            }

            public (decimal SoTien, string DienGiai) TinhTienDichVuCoDinh(decimal giaDv, decimal soLuong, string tenDichVu, DateTime batDau, DateTime? ketThuc, int thang, int nam, DateTime? contractStart = null, DateTime? contractEnd = null)
                => (giaDv * soLuong, tenDichVu);
        }

        private readonly FakeDienNuocStore _dienNuocStore;
        private readonly FakeHoaDonStore _hoaDonStore;
        private readonly FakeUnitOfWork _unitOfWork;
        private readonly FakeEmployeeBranchStore _employeeStore;
        private readonly EmployeeAccessService _accessService;
        private readonly FakeCalculatorService _calculatorService;
        private readonly FakeMeterReadingWorkflowService _workflowService;
        private readonly DienNuocService _dienNuocService;

        public MeterReadingCorrectionTests()
        {
            _dienNuocStore = new FakeDienNuocStore();
            _hoaDonStore = new FakeHoaDonStore();
            _unitOfWork = new FakeUnitOfWork();
            _employeeStore = new FakeEmployeeBranchStore();
            _accessService = new EmployeeAccessService(_employeeStore);
            _calculatorService = new FakeCalculatorService();
            _workflowService = new FakeMeterReadingWorkflowService(_dienNuocStore, _unitOfWork, _hoaDonStore, _calculatorService);

            _dienNuocService = new DienNuocService(
                _dienNuocStore,
                _accessService,
                _workflowService);

            // Thiết lập phòng 101 thuộc CN 1
            _dienNuocStore.RoomBranches[101] = 1;
            _dienNuocStore.RoomNames[101] = "101";

            // Nhân viên 10 thuộc CN 1
            _employeeStore.Actors[10] = new EmployeeBranchActorDto
            {
                NguoiDungId = 10,
                Role = Role.NhanVien,
                IsActive = true,
                ActiveBranchIds = new List<int> { 1 }
            };
        }

        [Fact]
        public async Task DaDuyet_NoInvoice_UpdatesMeterReading_Successfully()
        {
            // Bản ghi chỉ số đã duyệt nhưng chưa có hóa đơn nào
            var record = new DichVuDienNuocCuaPhong
            {
                DichVuDienNuocCuaPhongId = 1,
                PhongTroId = 101,
                Thang = 9,
                Nam = 2026,
                ChiSoDienCu = 100m,
                ChiSoDienMoi = 150m,
                ChiSoNuocCu = 50m,
                ChiSoNuocMoi = 60m,
                DonGiaDien = 3500m,
                DonGiaNuoc = 20000m,
                TrangThaiGhiNhan = TrangThaiGhiNhan.DaDuyet
            };
            _dienNuocStore.CurrentMonthRecords[101] = record;
            _dienNuocStore.PreviousMonthRecords[101] = new DichVuDienNuocCuaPhong { ChiSoDienMoi = 100m, ChiSoNuocMoi = 50m };

            var input = new ChotDienNuocReq
            {
                ChiNhanhId = 1,
                Thang = 9,
                Nam = 2026,
                DanhSachPhong = new List<DienNuocPhongReq>
                {
                    new DienNuocPhongReq
                    {
                        PhongTroId = 101,
                        ChiSoDienCu = 100m,
                        ChiSoDienMoi = 160m, // Cập nhật từ 150 lên 160
                        ChiSoNuocCu = 50m,
                        ChiSoNuocMoi = 65m   // Cập nhật từ 60 lên 65
                    }
                }
            };

            var result = await _dienNuocService.SaveChotDienNuocAsync(input, actorId: 10);

            Assert.True(result.IsSuccess);
            Assert.Equal(160m, record.ChiSoDienMoi);
            Assert.Equal(65m, record.ChiSoNuocMoi);
            Assert.True(_unitOfWork.CurrentTransaction.Committed);
        }

        [Fact]
        public async Task DaDuyet_WithDraftInvoice_UpdatesReading_And_RecalculatesDraftElectricWater_And_Total()
        {
            var record = new DichVuDienNuocCuaPhong
            {
                DichVuDienNuocCuaPhongId = 1,
                PhongTroId = 101,
                Thang = 9,
                Nam = 2026,
                ChiSoDienCu = 100m,
                ChiSoDienMoi = 150m, // 50 * 3500 = 175,000
                ChiSoNuocCu = 50m,
                ChiSoNuocMoi = 60m,  // 10 * 20000 = 200,000
                DonGiaDien = 3500m,
                DonGiaNuoc = 20000m,
                TrangThaiGhiNhan = TrangThaiGhiNhan.DaDuyet
            };
            _dienNuocStore.CurrentMonthRecords[101] = record;
            _dienNuocStore.PreviousMonthRecords[101] = new DichVuDienNuocCuaPhong { ChiSoDienMoi = 100m, ChiSoNuocMoi = 50m };

            var hopDong = new HopDong
            {
                HopDongId = 1,
                PhongTroId = 101,
                TienThuePhong = 3_000_000m,
                ThoiDiemBatDau = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            };

            var invoice = new HoaDon
            {
                HoaDonId = 1,
                HopDongId = 1,
                HopDong = hopDong,
                DichVuDienNuocCuaPhongId = 1,
                Thang = 9,
                Nam = 2026,
                TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.Nhap,
                ChiTietHoaDonDichVus = new List<ChiTietHoaDon>
                {
                    new ChiTietHoaDon { TenDichVu = "Tiền thuê phòng", DonGia = 3_000_000m, SoLuong = 1, TongTien = 3_000_000m },
                    new ChiTietHoaDon
                    {
                        TenDichVu = "Tiền điện (100 -> 150)",
                        DonGia = 3500m,
                        SoLuong = 50,
                        TongTien = 175_000m,
                        DichVu = new DichVu { LoaiDichVu = LoaiDichVu.Dien, TenDichVu = "Điện" }
                    },
                    new ChiTietHoaDon
                    {
                        TenDichVu = "Tiền nước (50 -> 60)",
                        DonGia = 20000m,
                        SoLuong = 10,
                        TongTien = 200_000m,
                        DichVu = new DichVu { LoaiDichVu = LoaiDichVu.Nuoc, TenDichVu = "Nước" }
                    },
                    new ChiTietHoaDon { TenDichVu = "Phí dịch vụ chung", DonGia = 100_000m, SoLuong = 1, TongTien = 100_000m },
                    new ChiTietHoaDon { TenDichVu = "Sửa chữa sự cố: Vòi nước", DonGia = 50_000m, SoLuong = 1, TongTien = 50_000m }
                },
                TongTien = 3_525_000m // 3,000,000 + 175,000 + 200,000 + 100,000 + 50,000
            };
            _hoaDonStore.Invoices.Add(invoice);

            var input = new ChotDienNuocReq
            {
                ChiNhanhId = 1,
                Thang = 9,
                Nam = 2026,
                DanhSachPhong = new List<DienNuocPhongReq>
                {
                    new DienNuocPhongReq
                    {
                        PhongTroId = 101,
                        ChiSoDienCu = 100m,
                        ChiSoDienMoi = 200m, // 100 kWh * 3500 = 350,000
                        ChiSoNuocCu = 50m,
                        ChiSoNuocMoi = 70m   // 20 m3 * 20000 = 400,000
                    }
                }
            };

            var result = await _dienNuocService.SaveChotDienNuocAsync(input, actorId: 10);

            Assert.True(result.IsSuccess);
            Assert.Equal(200m, record.ChiSoDienMoi);
            Assert.Equal(70m, record.ChiSoNuocMoi);

            // Kiểm tra hóa đơn Nháp được tính lại đúng
            var dienDetail = invoice.ChiTietHoaDonDichVus.First(x => x.DichVu?.LoaiDichVu == LoaiDichVu.Dien);
            Assert.Equal(100m, dienDetail.SoLuong);
            Assert.Equal(350_000m, dienDetail.TongTien);

            var nuocDetail = invoice.ChiTietHoaDonDichVus.First(x => x.DichVu?.LoaiDichVu == LoaiDichVu.Nuoc);
            Assert.Equal(20m, nuocDetail.SoLuong);
            Assert.Equal(400_000m, nuocDetail.TongTien);

            // Các khoản cố định không đổi: Tiền phòng (3m), Phí dịch vụ (100k), Sự cố (50k)
            var roomDetail = invoice.ChiTietHoaDonDichVus.First(x => x.TenDichVu == "Tiền thuê phòng");
            Assert.Equal(3_000_000m, roomDetail.TongTien);
            var serviceDetail = invoice.ChiTietHoaDonDichVus.First(x => x.TenDichVu == "Phí dịch vụ chung");
            Assert.Equal(100_000m, serviceDetail.TongTien);
            var incidentDetail = invoice.ChiTietHoaDonDichVus.First(x => x.TenDichVu.StartsWith("Sửa chữa sự cố"));
            Assert.Equal(50_000m, incidentDetail.TongTien);

            // Tổng tiền mới = 3,000,000 + 350,000 + 400,000 + 100,000 + 50,000 = 3,900,000
            Assert.Equal(3_900_000m, invoice.TongTien);
            Assert.True(_unitOfWork.CurrentTransaction.Committed);
        }

        [Fact]
        public async Task DaDuyet_WithSubmittedInvoice_RejectsCorrection()
        {
            var record = new DichVuDienNuocCuaPhong
            {
                DichVuDienNuocCuaPhongId = 1,
                PhongTroId = 101,
                Thang = 9,
                Nam = 2026,
                ChiSoDienCu = 100m,
                ChiSoDienMoi = 150m,
                ChiSoNuocCu = 50m,
                ChiSoNuocMoi = 60m,
                TrangThaiGhiNhan = TrangThaiGhiNhan.DaDuyet
            };
            _dienNuocStore.CurrentMonthRecords[101] = record;
            _dienNuocStore.PreviousMonthRecords[101] = new DichVuDienNuocCuaPhong { ChiSoDienMoi = 100m, ChiSoNuocMoi = 50m };

            var invoice = new HoaDon
            {
                HoaDonId = 1,
                DichVuDienNuocCuaPhongId = 1,
                TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.ChoDuyet
            };
            _hoaDonStore.Invoices.Add(invoice);

            var input = new ChotDienNuocReq
            {
                ChiNhanhId = 1,
                Thang = 9,
                Nam = 2026,
                DanhSachPhong = new List<DienNuocPhongReq>
                {
                    new DienNuocPhongReq { PhongTroId = 101, ChiSoDienCu = 100m, ChiSoDienMoi = 180m, ChiSoNuocCu = 50m, ChiSoNuocMoi = 70m }
                }
            };

            var result = await _dienNuocService.SaveChotDienNuocAsync(input, actorId: 10);

            Assert.False(result.IsSuccess);
            Assert.Contains("khóa", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(150m, record.ChiSoDienMoi);
            Assert.True(_unitOfWork.CurrentTransaction.RolledBack);
        }

        [Theory]
        [InlineData(TrangThaiPhatHanhHoaDon.DaChot)]
        [InlineData(TrangThaiPhatHanhHoaDon.DaGui)]
        [InlineData(TrangThaiPhatHanhHoaDon.DaHuy)]
        public async Task DaDuyet_WithFinalizedOrSentOrCancelledInvoice_RejectsCorrection(TrangThaiPhatHanhHoaDon lockedStatus)
        {
            var record = new DichVuDienNuocCuaPhong
            {
                DichVuDienNuocCuaPhongId = 1,
                PhongTroId = 101,
                Thang = 9,
                Nam = 2026,
                ChiSoDienCu = 100m,
                ChiSoDienMoi = 150m,
                ChiSoNuocCu = 50m,
                ChiSoNuocMoi = 60m,
                TrangThaiGhiNhan = TrangThaiGhiNhan.DaDuyet
            };
            _dienNuocStore.CurrentMonthRecords[101] = record;
            _dienNuocStore.PreviousMonthRecords[101] = new DichVuDienNuocCuaPhong { ChiSoDienMoi = 100m, ChiSoNuocMoi = 50m };

            var invoice = new HoaDon
            {
                HoaDonId = 1,
                DichVuDienNuocCuaPhongId = 1,
                TrangThaiPhatHanh = lockedStatus
            };
            _hoaDonStore.Invoices.Add(invoice);

            var input = new ChotDienNuocReq
            {
                ChiNhanhId = 1,
                Thang = 9,
                Nam = 2026,
                DanhSachPhong = new List<DienNuocPhongReq>
                {
                    new DienNuocPhongReq { PhongTroId = 101, ChiSoDienCu = 100m, ChiSoDienMoi = 180m, ChiSoNuocCu = 50m, ChiSoNuocMoi = 70m }
                }
            };

            var result = await _dienNuocService.SaveChotDienNuocAsync(input, actorId: 10);

            Assert.False(result.IsSuccess);
            Assert.Contains("khóa", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(150m, record.ChiSoDienMoi);
            Assert.True(_unitOfWork.CurrentTransaction.RolledBack);
        }

        [Fact]
        public async Task WithLockedSubsequentMonth_RejectsCorrection()
        {
            var record = new DichVuDienNuocCuaPhong
            {
                DichVuDienNuocCuaPhongId = 1,
                PhongTroId = 101,
                Thang = 9,
                Nam = 2026,
                ChiSoDienCu = 100m,
                ChiSoDienMoi = 150m,
                ChiSoNuocCu = 50m,
                ChiSoNuocMoi = 60m,
                TrangThaiGhiNhan = TrangThaiGhiNhan.DaDuyet
            };
            _dienNuocStore.CurrentMonthRecords[101] = record;
            _dienNuocStore.PreviousMonthRecords[101] = new DichVuDienNuocCuaPhong { ChiSoDienMoi = 100m, ChiSoNuocMoi = 50m };
            _dienNuocStore.LockedRooms.Add(101); // Tháng 10 đã chốt

            var input = new ChotDienNuocReq
            {
                ChiNhanhId = 1,
                Thang = 9,
                Nam = 2026,
                DanhSachPhong = new List<DienNuocPhongReq>
                {
                    new DienNuocPhongReq { PhongTroId = 101, ChiSoDienCu = 100m, ChiSoDienMoi = 180m, ChiSoNuocCu = 50m, ChiSoNuocMoi = 70m }
                }
            };

            var result = await _dienNuocService.SaveChotDienNuocAsync(input, actorId: 10);

            Assert.False(result.IsSuccess);
            Assert.Contains("tháng tiếp theo đã được chốt", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(150m, record.ChiSoDienMoi);
            Assert.True(_unitOfWork.CurrentTransaction.RolledBack);
        }

        [Fact]
        public async Task BatchFailure_AtSecondRoom_RollsBackAllChanges()
        {
            _dienNuocStore.RoomBranches[102] = 1;
            _dienNuocStore.RoomNames[102] = "102";

            var record1 = new DichVuDienNuocCuaPhong
            {
                DichVuDienNuocCuaPhongId = 1,
                PhongTroId = 101,
                Thang = 9,
                Nam = 2026,
                ChiSoDienCu = 100m,
                ChiSoDienMoi = 150m,
                ChiSoNuocCu = 50m,
                ChiSoNuocMoi = 60m,
                TrangThaiGhiNhan = TrangThaiGhiNhan.DaDuyet
            };
            var record2 = new DichVuDienNuocCuaPhong
            {
                DichVuDienNuocCuaPhongId = 2,
                PhongTroId = 102,
                Thang = 9,
                Nam = 2026,
                ChiSoDienCu = 200m,
                ChiSoDienMoi = 250m,
                ChiSoNuocCu = 80m,
                ChiSoNuocMoi = 90m,
                TrangThaiGhiNhan = TrangThaiGhiNhan.DaDuyet
            };
            _dienNuocStore.CurrentMonthRecords[101] = record1;
            _dienNuocStore.CurrentMonthRecords[102] = record2;
            _dienNuocStore.PreviousMonthRecords[101] = new DichVuDienNuocCuaPhong { ChiSoDienMoi = 100m, ChiSoNuocMoi = 50m };
            _dienNuocStore.PreviousMonthRecords[102] = new DichVuDienNuocCuaPhong { ChiSoDienMoi = 200m, ChiSoNuocMoi = 80m };

            // Phòng 102 có hóa đơn đã chốt -> lỗi ở phòng 102
            _hoaDonStore.Invoices.Add(new HoaDon
            {
                HoaDonId = 2,
                DichVuDienNuocCuaPhongId = 2,
                TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaChot
            });

            var input = new ChotDienNuocReq
            {
                ChiNhanhId = 1,
                Thang = 9,
                Nam = 2026,
                DanhSachPhong = new List<DienNuocPhongReq>
                {
                    new DienNuocPhongReq { PhongTroId = 101, ChiSoDienCu = 100m, ChiSoDienMoi = 180m, ChiSoNuocCu = 50m, ChiSoNuocMoi = 70m },
                    new DienNuocPhongReq { PhongTroId = 102, ChiSoDienCu = 200m, ChiSoDienMoi = 280m, ChiSoNuocCu = 80m, ChiSoNuocMoi = 99m }
                }
            };

            var result = await _dienNuocService.SaveChotDienNuocAsync(input, actorId: 10);

            Assert.False(result.IsSuccess);
            Assert.True(_unitOfWork.CurrentTransaction.RolledBack);
            Assert.False(_unitOfWork.CurrentTransaction.Committed);
        }

        [Fact]
        public async Task DaDuyet_WithCancelledInvoice_RejectsCorrection_EvenWhenInvoiceIsSoftDeleted()
        {
            var record = new DichVuDienNuocCuaPhong
            {
                DichVuDienNuocCuaPhongId = 1,
                PhongTroId = 101,
                Thang = 9,
                Nam = 2026,
                ChiSoDienCu = 100m,
                ChiSoDienMoi = 150m,
                ChiSoNuocCu = 50m,
                ChiSoNuocMoi = 60m,
                TrangThaiGhiNhan = TrangThaiGhiNhan.DaDuyet
            };
            _dienNuocStore.CurrentMonthRecords[101] = record;
            _dienNuocStore.PreviousMonthRecords[101] = new DichVuDienNuocCuaPhong { ChiSoDienMoi = 100m, ChiSoNuocMoi = 50m };
            _hoaDonStore.Invoices.Add(new HoaDon
            {
                HoaDonId = 1,
                DichVuDienNuocCuaPhongId = 1,
                TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaHuy,
                IsDeleted = true
            });

            var input = new ChotDienNuocReq
            {
                ChiNhanhId = 1,
                Thang = 9,
                Nam = 2026,
                DanhSachPhong = new List<DienNuocPhongReq>
                {
                    new() { PhongTroId = 101, ChiSoDienCu = 100m, ChiSoDienMoi = 200m, ChiSoNuocCu = 50m, ChiSoNuocMoi = 70m }
                }
            };

            var result = await _dienNuocService.SaveChotDienNuocAsync(input, actorId: 10);

            Assert.False(result.IsSuccess);
            Assert.Equal(150m, record.ChiSoDienMoi);
            Assert.Equal(60m, record.ChiSoNuocMoi);
            Assert.True(_unitOfWork.CurrentTransaction.RolledBack);
            Assert.False(_unitOfWork.CurrentTransaction.Committed);
        }

        [Fact]
        public async Task DraftInvoice_AddsMissingElectricAndWaterLines_WhenCorrectionChangesZeroUsageToPositive()
        {
            var record = new DichVuDienNuocCuaPhong
            {
                DichVuDienNuocCuaPhongId = 1,
                PhongTroId = 101,
                Thang = 9,
                Nam = 2026,
                ChiSoDienCu = 100m,
                ChiSoDienMoi = 100m,
                ChiSoNuocCu = 50m,
                ChiSoNuocMoi = 50m,
                DonGiaDien = 3500m,
                DonGiaNuoc = 20000m,
                TrangThaiGhiNhan = TrangThaiGhiNhan.DaDuyet
            };
            _dienNuocStore.CurrentMonthRecords[101] = record;
            _dienNuocStore.PreviousMonthRecords[101] = new DichVuDienNuocCuaPhong { ChiSoDienMoi = 100m, ChiSoNuocMoi = 50m };

            var invoice = new HoaDon
            {
                HoaDonId = 1,
                HopDongId = 1,
                HopDong = new HopDong
                {
                    HopDongId = 1,
                    PhongTroId = 101,
                    TienThuePhong = 3_000_000m,
                    ThoiDiemBatDau = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                },
                DichVuDienNuocCuaPhongId = 1,
                Thang = 9,
                Nam = 2026,
                TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.Nhap,
                TongTien = 3_000_000m,
                ChiTietHoaDonDichVus = new List<ChiTietHoaDon>
                {
                    new() { TenDichVu = "Tiền thuê phòng", SoLuong = 1, DonGia = 3_000_000m, TongTien = 3_000_000m }
                }
            };
            _hoaDonStore.Invoices.Add(invoice);

            var input = new ChotDienNuocReq
            {
                ChiNhanhId = 1,
                Thang = 9,
                Nam = 2026,
                DanhSachPhong = new List<DienNuocPhongReq>
                {
                    new() { PhongTroId = 101, ChiSoDienCu = 100m, ChiSoDienMoi = 120m, ChiSoNuocCu = 50m, ChiSoNuocMoi = 55m }
                }
            };

            var result = await _dienNuocService.SaveChotDienNuocAsync(input, actorId: 10);

            Assert.True(result.IsSuccess);
            var electric = invoice.ChiTietHoaDonDichVus.Single(x => x.TenDichVu.StartsWith("Tiền điện", StringComparison.OrdinalIgnoreCase));
            var water = invoice.ChiTietHoaDonDichVus.Single(x => x.TenDichVu.StartsWith("Tiền nước", StringComparison.OrdinalIgnoreCase));
            Assert.Equal(70_000m, electric.TongTien);
            Assert.Equal(100_000m, water.TongTien);
            Assert.Equal(3_170_000m, invoice.TongTien);
        }
    }
}
