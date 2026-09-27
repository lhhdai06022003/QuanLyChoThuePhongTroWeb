using System;
using System.Collections.Generic;
using System.IO;
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
    public class SaveChotDienNuocWorkflowTests
    {
        private class FakeUnitOfWork : IUnitOfWork
        {
            public Task<IApplicationTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
                => Task.FromResult<IApplicationTransaction>(new FakeTransaction());

            public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
                => Task.FromResult(1);

            private class FakeTransaction : IApplicationTransaction
            {
                public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
                public Task RollbackAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
                public void Dispose() { }
                public ValueTask DisposeAsync() => ValueTask.CompletedTask;
            }
        }

        private class FakeDienNuocStore : IDienNuocStore
        {
            public Dictionary<int, int> RoomBranches { get; set; } = new() { { 101, 1 } };
            public Dictionary<int, string> RoomNames { get; set; } = new() { { 101, "101" } };

            public Task<IReadOnlyList<HopDong>> GetActiveContractsInBranchAsync(int chiNhanhId, DateTime startOfMonth, DateTime endOfMonth, CancellationToken cancellationToken = default)
                => Task.FromResult<IReadOnlyList<HopDong>>(new List<HopDong>());

            public Task<IReadOnlyDictionary<int, DichVuDienNuocCuaPhong>> GetCurrentMonthRecordsAsync(IReadOnlyList<int> roomIds, int thang, int nam, CancellationToken cancellationToken = default)
                => Task.FromResult<IReadOnlyDictionary<int, DichVuDienNuocCuaPhong>>(new Dictionary<int, DichVuDienNuocCuaPhong>());

            public Task<IReadOnlyDictionary<int, DichVuDienNuocCuaPhong>> GetPreviousMonthRecordsAsync(IReadOnlyList<int> roomIds, int prevThang, int prevNam, CancellationToken cancellationToken = default)
                => Task.FromResult<IReadOnlyDictionary<int, DichVuDienNuocCuaPhong>>(new Dictionary<int, DichVuDienNuocCuaPhong>());

            public Task<ISet<int>> GetLockedRoomIdsAsync(IReadOnlyList<int> roomIds, int thang, int nam, CancellationToken cancellationToken = default)
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

            public void UpdateRecord(DichVuDienNuocCuaPhong record) { }
            public Task AddRecordAsync(DichVuDienNuocCuaPhong record, CancellationToken cancellationToken = default) => Task.CompletedTask;
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

        private class FakeHoaDonStore : IHoaDonStore
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
        private readonly FakeEmployeeBranchStore _employeeStore;
        private readonly EmployeeAccessService _accessService;
        private readonly FakeMeterReadingWorkflowService _workflowService;

        public SaveChotDienNuocWorkflowTests()
        {
            _dienNuocStore = new FakeDienNuocStore();
            _employeeStore = new FakeEmployeeBranchStore();
            _accessService = new EmployeeAccessService(_employeeStore);
            _workflowService = new FakeMeterReadingWorkflowService();

            _employeeStore.Actors[10] = new EmployeeBranchActorDto
            {
                NguoiDungId = 10,
                Role = Role.NhanVien,
                IsActive = true,
                ActiveBranchIds = new List<int> { 1 }
            };
        }

        [Fact]
        public async Task SaveChotDienNuoc_DelegatesToWorkflow_AndRejects_WhenManualInputWithoutReason()
        {
            _workflowService.OnApproveBatch = (req, actor) =>
            {
                var item = req.DanhSachPhong.FirstOrDefault();
                if (item != null && string.IsNullOrWhiteSpace(item.LyDoDienThuCong))
                {
                    return ServiceResult<MeterPeriodsApprovalResult>.Fail("Bắt buộc phải có lý do khi nhập chỉ số điện thủ công.");
                }
                return ServiceResult<MeterPeriodsApprovalResult>.Ok(new MeterPeriodsApprovalResult());
            };

            var service = new DienNuocService(
                _dienNuocStore,
                _accessService,
                _workflowService);

            var req = new ChotDienNuocReq
            {
                ChiNhanhId = 1,
                Thang = 9,
                Nam = 2026,
                DanhSachPhong = new List<DienNuocPhongReq>
                {
                    new()
                    {
                        PhongTroId = 101,
                        ChiSoDienCu = 100,
                        ChiSoDienMoi = 150,
                        ChiSoNuocCu = 50,
                        ChiSoNuocMoi = 60,
                        LyDoNhapThuCongDien = null,
                        LyDoNhapThuCongNuoc = null
                    }
                }
            };

            var result = await service.SaveChotDienNuocAsync(req, actorId: 10);

            Assert.False(result.IsSuccess);
            Assert.Contains("lý do", result.ErrorMessage);
            Assert.Single(_workflowService.ApprovedBatchRequests);
        }

        [Fact]
        public async Task SaveChotDienNuoc_DelegatesToWorkflow_Succeeds_WhenManualInputHasReason()
        {
            var service = new DienNuocService(
                _dienNuocStore,
                _accessService,
                _workflowService);

            var req = new ChotDienNuocReq
            {
                ChiNhanhId = 1,
                Thang = 9,
                Nam = 2026,
                DanhSachPhong = new List<DienNuocPhongReq>
                {
                    new()
                    {
                        PhongTroId = 101,
                        ChiSoDienCu = 100,
                        ChiSoDienMoi = 150,
                        ChiSoNuocCu = 50,
                        ChiSoNuocMoi = 60,
                        LyDoNhapThuCongDien = "Đồng hồ kẹt kim",
                        LyDoNhapThuCongNuoc = "Đồng hồ mờ số"
                    }
                }
            };

            var result = await service.SaveChotDienNuocAsync(req, actorId: 10);

            Assert.True(result.IsSuccess);
            Assert.Single(_workflowService.ApprovedBatchRequests);
            var approvedItem = Assert.Single(_workflowService.ApprovedBatchRequests[0].DanhSachPhong);
            Assert.Equal("Đồng hồ kẹt kim", approvedItem.LyDoDienThuCong);
            Assert.Equal("Đồng hồ mờ số", approvedItem.LyDoNuocThuCong);
        }
    }
}
