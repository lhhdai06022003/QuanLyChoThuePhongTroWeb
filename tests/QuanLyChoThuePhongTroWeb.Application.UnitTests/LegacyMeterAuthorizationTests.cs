using System;
using System.Collections.Generic;
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
    public class LegacyMeterAuthorizationTests
    {
        private class FakeApplicationTransaction : IApplicationTransaction
        {
            public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
            public Task RollbackAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
            public void Dispose() { }
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }

        private class FakeUnitOfWork : IUnitOfWork
        {
            public Task<IApplicationTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult<IApplicationTransaction>(new FakeApplicationTransaction());
            }

            public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
        }

        private class FakeDienNuocStore : IDienNuocStore
        {
            public Dictionary<int, int> RoomBranches { get; set; } = new();
            public Dictionary<int, string> RoomNames { get; set; } = new();
            public Dictionary<int, DichVuDienNuocCuaPhong> CurrentMonthRecords { get; set; } = new();
            public Dictionary<int, DichVuDienNuocCuaPhong> PreviousMonthRecords { get; set; } = new();
            public HashSet<int> LockedRooms { get; set; } = new();
            public List<DichVuDienNuocCuaPhong> SavedRecords { get; } = new();

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
                => Task.FromResult(3500m);

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

            public Task AddRecordAsync(DichVuDienNuocCuaPhong record, CancellationToken cancellationToken = default)
            {
                SavedRecords.Add(record);
                return Task.CompletedTask;
            }

            public void UpdateRecord(DichVuDienNuocCuaPhong record)
            {
                SavedRecords.Add(record);
            }
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

        private readonly FakeDienNuocStore _dienNuocStore;
        private readonly FakeEmployeeBranchStore _employeeStore;
        private readonly EmployeeAccessService _accessService;
        private readonly DienNuocService _dienNuocService;

        private class FakeHoaDonStoreForMeter : IHoaDonStore
        {
            public Task<IReadOnlyList<HoaDon>> GetInvoicesByMeterReadingIdsAsync(IReadOnlyList<int> meterReadingIds, CancellationToken cancellationToken = default)
                => Task.FromResult<IReadOnlyList<HoaDon>>(new List<HoaDon>());

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

        private class FakeCalculatorServiceForMeter : IHoaDonCalculatorService
        {
            public (decimal SoTien, string DienGiai, int SoNgayO) TinhTienPhong(decimal giaThue, DateTime batDau, DateTime? ketThuc, int thang, int nam) => (giaThue, "", 30);
            public (decimal SoLuong, decimal SoTien, string DienGiai) TinhTienDienNuoc(decimal chiSoMoi, decimal chiSoCu, decimal donGia, string tenDichVu, int soNgayO, int tongNgayTrongThang) => (chiSoMoi - chiSoCu, (chiSoMoi - chiSoCu) * donGia, "");
            public (decimal SoTien, string DienGiai) TinhTienDichVuCoDinh(decimal giaDv, decimal soLuong, string tenDichVu, DateTime batDau, DateTime? ketThuc, int thang, int nam, DateTime? contractStart = null, DateTime? contractEnd = null) => (giaDv * soLuong, "");
        }

        public LegacyMeterAuthorizationTests()
        {
            _dienNuocStore = new FakeDienNuocStore();
            _employeeStore = new FakeEmployeeBranchStore();
            _accessService = new EmployeeAccessService(_employeeStore);
            var uow = new FakeUnitOfWork();
            var workflowService = new FakeMeterReadingWorkflowService(_dienNuocStore, uow);
            _dienNuocService = new DienNuocService(
                _dienNuocStore,
                _accessService,
                workflowService);

            // Phòng 101 và 102 thuộc chi nhánh 1
            _dienNuocStore.RoomBranches[101] = 1;
            _dienNuocStore.RoomBranches[102] = 1;
            _dienNuocStore.RoomNames[101] = "P101";
            _dienNuocStore.RoomNames[102] = "P102";

            // Phòng 201 thuộc chi nhánh 2
            _dienNuocStore.RoomBranches[201] = 2;
            _dienNuocStore.RoomNames[201] = "P201";

            // Nhân viên 10: phụ trách chi nhánh 1
            _employeeStore.Actors[10] = new EmployeeBranchActorDto
            {
                NguoiDungId = 10,
                Role = Role.NhanVien,
                IsActive = true,
                ActiveBranchIds = new List<int> { 1 }
            };

            // Nhân viên 20: phụ trách chi nhánh 2
            _employeeStore.Actors[20] = new EmployeeBranchActorDto
            {
                NguoiDungId = 20,
                Role = Role.NhanVien,
                IsActive = true,
                ActiveBranchIds = new List<int> { 2 }
            };

            // Nhân viên 30: không phụ trách chi nhánh nào
            _employeeStore.Actors[30] = new EmployeeBranchActorDto
            {
                NguoiDungId = 30,
                Role = Role.NhanVien,
                IsActive = true,
                ActiveBranchIds = new List<int>()
            };

            // Admin 1: toàn quyền
            _employeeStore.Actors[1] = new EmployeeBranchActorDto
            {
                NguoiDungId = 1,
                Role = Role.Admin,
                IsActive = true,
                ActiveBranchIds = new List<int>()
            };
        }

        [Fact]
        public async Task Staff_WithoutBranchAssignment_CannotSaveChotDienNuoc()
        {
            var req = new ChotDienNuocReq
            {
                ChiNhanhId = 1,
                Thang = 9,
                Nam = 2026,
                DanhSachPhong = new List<DienNuocPhongReq>
                {
                    new DienNuocPhongReq() { PhongTroId = 101, ChiSoDienCu = 10, ChiSoDienMoi = 20, ChiSoNuocCu = 5, ChiSoNuocMoi = 10 }
                }
            };

            var result = await _dienNuocService.SaveChotDienNuocAsync(req, actorId: 30);

            Assert.False(result.IsSuccess);
            Assert.Contains("không có quyền", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Staff_AssignedToBranch2_CannotSaveChotDienNuoc_ForBranch1_EvenIfFormFakesBranchId()
        {
            // Nhân viên 20 giả mạo gửi ChiNhanhId = 2 nhưng DanhSachPhong chứa phòng 101 (thuộc chi nhánh 1)
            var req = new ChotDienNuocReq
            {
                ChiNhanhId = 2,
                Thang = 9,
                Nam = 2026,
                DanhSachPhong = new List<DienNuocPhongReq>
                {
                    new DienNuocPhongReq() { PhongTroId = 101, ChiSoDienCu = 10, ChiSoDienMoi = 20, ChiSoNuocCu = 5, ChiSoNuocMoi = 10 }
                }
            };

            var result = await _dienNuocService.SaveSaveChotDienNuocAsyncSafe(req, actorId: 20);

            Assert.False(result.IsSuccess);
            // Bị phát hiện vì chi nhánh yêu cầu không khớp với chi nhánh thực tế của phòng trong DB
            Assert.Contains("không khớp", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Staff_AssignedToBranch2_CannotSaveChotDienNuoc_WhenAccuratelySpecifyingBranch1()
        {
            // Nhân viên 20 gửi ChiNhanhId = 1 cho phòng 101 (chi nhánh 1), nhưng nhân viên chỉ có quyền chi nhánh 2
            var req = new ChotDienNuocReq
            {
                ChiNhanhId = 1,
                Thang = 9,
                Nam = 2026,
                DanhSachPhong = new List<DienNuocPhongReq>
                {
                    new DienNuocPhongReq() { PhongTroId = 101, ChiSoDienCu = 10, ChiSoDienMoi = 20, ChiSoNuocCu = 5, ChiSoNuocMoi = 10 }
                }
            };

            var result = await _dienNuocService.SaveChotDienNuocAsync(req, actorId: 20);

            Assert.False(result.IsSuccess);
            Assert.Contains("không có quyền", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Staff_AssignedToBranch1_CanSaveChotDienNuoc_ForBranch1()
        {
            var req = new ChotDienNuocReq
            {
                ChiNhanhId = 1,
                Thang = 9,
                Nam = 2026,
                DanhSachPhong = new List<DienNuocPhongReq>
                {
                    new DienNuocPhongReq() { PhongTroId = 101, ChiSoDienCu = 0, ChiSoDienMoi = 20, ChiSoNuocCu = 0, ChiSoNuocMoi = 10 }
                }
            };

            var result = await _dienNuocService.SaveChotDienNuocAsync(req, actorId: 10);

            Assert.True(result.IsSuccess, result.ErrorMessage);
            Assert.Single(_dienNuocStore.SavedRecords);
        }

        [Fact]
        public async Task Admin_CanSaveChotDienNuoc_AtAnyBranch()
        {
            var req = new ChotDienNuocReq
            {
                ChiNhanhId = 1,
                Thang = 9,
                Nam = 2026,
                DanhSachPhong = new List<DienNuocPhongReq>
                {
                    new DienNuocPhongReq() { PhongTroId = 101, ChiSoDienCu = 0, ChiSoDienMoi = 20, ChiSoNuocCu = 0, ChiSoNuocMoi = 10 }
                }
            };

            var result = await _dienNuocService.SaveChotDienNuocAsync(req, actorId: 1);

            Assert.True(result.IsSuccess, result.ErrorMessage);
            Assert.Single(_dienNuocStore.SavedRecords);
        }

        [Fact]
        public async Task SaveChotDienNuoc_Rejects_WhenRoomsBelongToMultipleBranches()
        {
            // Danh sách phòng trộn lẫn phòng 101 (CN 1) và 201 (CN 2)
            var req = new ChotDienNuocReq
            {
                ChiNhanhId = 1,
                Thang = 9,
                Nam = 2026,
                DanhSachPhong = new List<DienNuocPhongReq>
                {
                    new DienNuocPhongReq() { PhongTroId = 101, ChiSoDienCu = 10, ChiSoDienMoi = 20, ChiSoNuocCu = 5, ChiSoNuocMoi = 10 },
                    new DienNuocPhongReq() { PhongTroId = 201, ChiSoDienCu = 10, ChiSoDienMoi = 20, ChiSoNuocCu = 5, ChiSoNuocMoi = 10 }
                }
            };

            var result = await _dienNuocService.SaveChotDienNuocAsync(req, actorId: 1);

            Assert.False(result.IsSuccess);
            Assert.Contains("nhiều chi nhánh", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task SaveChotDienNuoc_Rejects_WhenRoomDoesNotExist()
        {
            var req = new ChotDienNuocReq
            {
                ChiNhanhId = 1,
                Thang = 9,
                Nam = 2026,
                DanhSachPhong = new List<DienNuocPhongReq>
                {
                    new DienNuocPhongReq() { PhongTroId = 9999, ChiSoDienCu = 10, ChiSoDienMoi = 20, ChiSoNuocCu = 5, ChiSoNuocMoi = 10 }
                }
            };

            var result = await _dienNuocService.SaveChotDienNuocAsync(req, actorId: 1);

            Assert.False(result.IsSuccess);
            Assert.Contains("không tồn tại", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task GetDanhSachDienNuoc_ReturnsEmpty_WhenStaffNotAssignedToBranch()
        {
            // Nhân viên 20 (chỉ thuộc CN 2) xem CN 1
            var data = await _dienNuocService.GetDanhSachDienNuocAsync(chiNhanhId: 1, thang: 9, nam: 2026, actorId: 20);

            Assert.Empty(data);
        }

        [Fact]
        public async Task GetDanhSachDienNuoc_Allows_WhenStaffAssignedToBranch()
        {
            // Nhân viên 10 (thuộc CN 1) xem CN 1
            var data = await _dienNuocService.GetDanhSachDienNuocAsync(chiNhanhId: 1, thang: 9, nam: 2026, actorId: 10);

            Assert.NotNull(data);
        }
    }

    internal static class TestExtensions
    {
        public static Task<(bool IsSuccess, string? ErrorMessage)> SaveSaveChotDienNuocAsyncSafe(this IDienNuocService service, ChotDienNuocReq req, int actorId)
            => service.SaveChotDienNuocAsync(req, actorId);
    }
}
