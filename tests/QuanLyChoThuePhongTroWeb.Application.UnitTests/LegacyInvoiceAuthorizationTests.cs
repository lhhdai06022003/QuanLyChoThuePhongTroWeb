using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Security;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Services;
using QuanLyChoThuePhongTroWeb.Application.Common.Configurations;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Common.Security;
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
    public class LegacyInvoiceAuthorizationTests
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
                => Task.FromResult<IApplicationTransaction>(new FakeApplicationTransaction());

            public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
        }

        private class FakeHoaDonStore : IHoaDonStore
        {
            public Dictionary<int, int> RoomBranches { get; set; } = new();
            public Dictionary<int, int> InvoiceBranches { get; set; } = new();
            public Dictionary<int, HoaDon> Invoices { get; set; } = new();
            public int GetPhongTrosCalls { get; private set; }
            public int DataTableCalls { get; private set; }
            public int UnpaidInvoicesCalls { get; private set; }

            public Task<ChiNhanh?> GetChiNhanhByIdAsync(int chiNhanhId, CancellationToken cancellationToken = default)
            {
                return Task.FromResult<ChiNhanh?>(new ChiNhanh { ChiNhanhId = chiNhanhId, TenChiNhanh = "CN " + chiNhanhId, MaChiNhanh = "CN" + chiNhanhId });
            }

            public Task<IReadOnlyList<HopDong>> GetValidContractsForBillingAsync(int chiNhanhId, IReadOnlyList<int> roomIds, DateTime startUtc, DateTime endUtc, CancellationToken cancellationToken = default)
                => Task.FromResult<IReadOnlyList<HopDong>>(new List<HopDong>());

            public Task<IReadOnlyList<int>> GetExistingInvoiceContractIdsAsync(IReadOnlyList<int> contractIds, int thang, int nam, CancellationToken cancellationToken = default)
                => Task.FromResult<IReadOnlyList<int>>(new List<int>());

            public Task<IReadOnlyList<DichVuDienNuocCuaPhong>> GetDichVuDienNuocByRoomIdsAsync(IReadOnlyList<int> roomIds, int thang, int nam, CancellationToken cancellationToken = default)
                => Task.FromResult<IReadOnlyList<DichVuDienNuocCuaPhong>>(new List<DichVuDienNuocCuaPhong>());

            public Task<IReadOnlyList<DangKyDichVu>> GetDangKyDichVusForBillingAsync(IReadOnlyList<int> roomIds, DateTime startUtc, DateTime endUtc, CancellationToken cancellationToken = default)
                => Task.FromResult<IReadOnlyList<DangKyDichVu>>(new List<DangKyDichVu>());

            public Task<IReadOnlyList<YeuCauSuCo>> GetBillableSuCosAsync(IReadOnlyList<int> roomIds, CancellationToken cancellationToken = default)
                => Task.FromResult<IReadOnlyList<YeuCauSuCo>>(new List<YeuCauSuCo>());

            public Task<IReadOnlyList<PhongTro>> GetPhongTrosByChiNhanhIdAsync(int chiNhanhId, CancellationToken cancellationToken = default)
            {
                GetPhongTrosCalls++;
                return Task.FromResult<IReadOnlyList<PhongTro>>(new List<PhongTro>());
            }

            public Task<HoaDon?> GetHoaDonWithDetailsForUpdateAsync(int id, CancellationToken cancellationToken = default)
            {
                Invoices.TryGetValue(id, out var hd);
                return Task.FromResult(hd);
            }

            public Task<DataTableResponse<HoaDonRes>> GetHoaDonsDataTableAsync(DataTableRequest request, int chiNhanhId, int thang, int nam, int trangThai, IReadOnlyList<int>? allowedBranchIds = null, CancellationToken cancellationToken = default)
            {
                DataTableCalls++;
                return Task.FromResult(new DataTableResponse<HoaDonRes>());
            }

            public Dictionary<int, HoaDonChiTietRes> InvoiceDetails { get; } = new();

            public Task<HoaDonChiTietRes?> GetHoaDonDetailByIdAsync(int id, CancellationToken cancellationToken = default)
            {
                InvoiceDetails.TryGetValue(id, out var detail);
                return Task.FromResult<HoaDonChiTietRes?>(detail);
            }

            public Task<HoaDon?> GetActiveHoaDonByIdAsync(int id, CancellationToken cancellationToken = default)
            {
                Invoices.TryGetValue(id, out var hd);
                return Task.FromResult(hd);
            }

            public Task<IReadOnlyList<HoaDonRes>> GetUnpaidInvoicesAsync(int chiNhanhId, int thang, int nam, CancellationToken cancellationToken = default)
            {
                UnpaidInvoicesCalls++;
                return Task.FromResult<IReadOnlyList<HoaDonRes>>(new List<HoaDonRes>());
            }

            public Task<IReadOnlyList<int>> GetContractIdsByTenantIdAsync(int nguoiThueId, CancellationToken cancellationToken = default)
                => Task.FromResult<IReadOnlyList<int>>(new List<int>());

            public Task<IReadOnlyList<HoaDonRes>> GetInvoicesByContractIdsAsync(IReadOnlyList<int> contractIds, CancellationToken cancellationToken = default)
                => Task.FromResult<IReadOnlyList<HoaDonRes>>(new List<HoaDonRes>());

            public Task<bool> CheckHoaDonOwnershipAsync(int hoaDonId, int nguoiThueId, CancellationToken cancellationToken = default)
                => Task.FromResult(false);

            public Task<IReadOnlyList<HoaDon>> GetOverdueInvoicesAsync(DateTime thresholdUtc, CancellationToken cancellationToken = default)
                => Task.FromResult<IReadOnlyList<HoaDon>>(new List<HoaDon>());

            public Task<int?> GetHoaDonBranchIdAsync(int hoaDonId, CancellationToken cancellationToken = default)
            {
                if (InvoiceBranches.TryGetValue(hoaDonId, out var branchId))
                {
                    return Task.FromResult<int?>(branchId);
                }
                return Task.FromResult<int?>(null);
            }

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

            public Task<IReadOnlyList<HoaDon>> GetInvoicesByMeterReadingIdsAsync(IReadOnlyList<int> meterReadingIds, CancellationToken cancellationToken = default)
            {
                var list = new List<HoaDon>();
                foreach (var hd in Invoices.Values)
                {
                    if (!hd.IsDeleted && hd.DichVuDienNuocCuaPhongId.HasValue && meterReadingIds.Contains(hd.DichVuDienNuocCuaPhongId.Value))
                    {
                        list.Add(hd);
                    }
                }
                return Task.FromResult<IReadOnlyList<HoaDon>>(list);
            }

            public Task<InvoiceCancellationBlockers> GetCancellationBlockersAsync(int hoaDonId, CancellationToken cancellationToken = default)
                => Task.FromResult(new InvoiceCancellationBlockers(false, false, false));

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
                => (giaThue, "Tiền phòng", 30);

            public (decimal SoLuong, decimal SoTien, string DienGiai) TinhTienDienNuoc(decimal chiSoMoi, decimal chiSoCu, decimal donGia, string tenDichVu, int soNgayO, int tongNgayTrongThang)
                => (10m, 100_000m, "Chi tiết ĐN");

            public (decimal SoTien, string DienGiai) TinhTienDichVuCoDinh(decimal giaDv, decimal soLuong, string tenDichVu, DateTime batDau, DateTime? ketThuc, int thang, int nam, DateTime? contractStart = null, DateTime? contractEnd = null)
                => (50_000m, "Chi tiết DV");
        }

        private class FakeDocumentExporter : IInvoiceDocumentExporter
        {
            public byte[] PdfBytes { get; set; } = new byte[] { 2 };
            public Exception? PdfException { get; set; }
            public byte[] ExportExcel(HoaDonChiTietRes hoaDon) => new byte[] { 1 };
            public byte[] ExportPdf(HoaDonChiTietRes hoaDon, byte[]? qrBytes, string bankId, string accountNumber, string accountName)
            {
                if (PdfException != null) throw PdfException;
                return PdfBytes;
            }
        }

        private class FakeVietQRService : IVietQRService
        {
            public string GenerateVietQRString(string bankId, string accountNumber, decimal amount, string memo) => "QR";
            public byte[] GenerateQRCodePNGBytes(string qrString) => new byte[] { 3 };
        }

        private class FakeEmailService : IEmailService
        {
            public int SendCount { get; private set; }
            public (bool IsSuccess, string ErrorMessage) Result { get; set; } = (true, string.Empty);
            public Exception? ExceptionToThrow { get; set; }

            public Task<(bool IsSuccess, string ErrorMessage)> SendInvoiceEmailAsync(string toEmail, HoaDonChiTietRes hoaDon, byte[] pdfBytes)
            {
                SendCount++;
                if (ExceptionToThrow != null) throw ExceptionToThrow;
                return Task.FromResult(Result);
            }
            public Task<(bool IsSuccess, string ErrorMessage)> SendContractExpiryAlertAsync(string toEmail, string tenNguoiNhan, QuanLyChoThuePhongTroWeb.Application.Features.Emails.DTOs.ContractExpiryAlertData alertData)
                => Task.FromResult((true, string.Empty));
        }

        private class CapturingLogger : ILogger<HoaDonService>
        {
            public List<string> Messages { get; } = new();

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
                => Messages.Add(formatter(state, exception));
        }

        private readonly FakeHoaDonStore _hoaDonStore;
        private readonly FakeEmployeeBranchStore _employeeStore;
        private readonly EmployeeAccessService _accessService;
        private readonly HoaDonService _hoaDonService;
        private readonly InvoiceIssuanceService _invoiceIssuanceService;
        private class FakeInvoiceIssuanceStore : IInvoiceIssuanceStore
        {
            public Task<IReadOnlyList<int>> LockContractsAsync(IReadOnlyList<int> hopDongIds, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<int>>(hopDongIds);
            public Task<HoaDon?> GetInvoiceForUpdateAsync(int hoaDonId, CancellationToken ct = default) => Task.FromResult<HoaDon?>(null);
            public Task<int> CountCancelledInvoicesAsync(int hopDongId, int thang, int nam, CancellationToken ct = default) => Task.FromResult(0);
            public Task<bool> HasOtherActiveInvoiceAsync(int hopDongId, int thang, int nam, int excludeHoaDonId, CancellationToken ct = default) => Task.FromResult(false);
            public Task AddInvoiceAsync(HoaDon hoaDon, CancellationToken ct = default) => Task.CompletedTask;
            public Task<IReadOnlyList<YeuCauSuCo>> GetBillableIncidentsInPeriodAsync(IReadOnlyList<int> roomIds, DateTime startUtc, DateTime nextMonthStartUtc, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<YeuCauSuCo>>(new List<YeuCauSuCo>());
            public Task<HoaDon?> GetActiveInvoiceForTenantRoomPeriodForUpdateAsync(int phongTroId, int nguoiThueId, int thang, int nam, CancellationToken ct = default) => Task.FromResult<HoaDon?>(null);
            public Task<(int? MeterPeriodId, int ChiNhanhId)?> GetInvoiceLockTargetsAsync(int hoaDonId, CancellationToken ct = default) => Task.FromResult<(int? MeterPeriodId, int ChiNhanhId)?>(null);
            public Task<TrangThaiGhiNhan?> LockMeterPeriodAsync(int meterPeriodId, CancellationToken ct = default) => Task.FromResult<TrangThaiGhiNhan?>(null);
            public Task<IReadOnlyList<int>> GetContractIdsForTenantRoomAsync(int phongTroId, int nguoiThueId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<int>>(new List<int>());
        }

        private readonly FakeDocumentExporter _documentExporter;
        private readonly FakeEmailService _emailService;
        private readonly CapturingLogger _logger;

        public LegacyInvoiceAuthorizationTests()
        {
            _hoaDonStore = new FakeHoaDonStore();
            _employeeStore = new FakeEmployeeBranchStore();
            _accessService = new EmployeeAccessService(_employeeStore);
            _documentExporter = new FakeDocumentExporter();
            _emailService = new FakeEmailService();
            _logger = new CapturingLogger();
            var issuanceStore = new FakeInvoiceIssuanceStore();
            _hoaDonService = new HoaDonService(
                _hoaDonStore,
                new FakeUnitOfWork(),
                new VietQrSettings(),
                _logger,
                new FakeCalculatorService(),
                _documentExporter,
                new FakeVietQRService(),
                _accessService,
                _emailService);
            _invoiceIssuanceService = new InvoiceIssuanceService(
                issuanceStore,
                _hoaDonStore,
                new FakeUnitOfWork(),
                _accessService,
                new FakeCalculatorService(),
                Microsoft.Extensions.Logging.Abstractions.NullLogger<InvoiceIssuanceService>.Instance);

            // Phòng 101 thuộc CN 1, phòng 201 thuộc CN 2
            _hoaDonStore.RoomBranches[101] = 1;
            _hoaDonStore.RoomBranches[201] = 2;

            // Hóa đơn 1 thuộc CN 1, hóa đơn 2 thuộc CN 2
            _hoaDonStore.InvoiceBranches[1] = 1;
            _hoaDonStore.InvoiceBranches[2] = 2;

            _hoaDonStore.Invoices[1] = new HoaDon
            {
                HoaDonId = 1,
                TrangThaiHoaDon = TrangThaiHoaDon.ChuaThanhToan,
                TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaChot,
                TongTien = 1_000_000m
            };

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
        public async Task PhatSinhHoaDon_Rejects_WhenStaffNotAssignedToBranch()
        {
            // Nhân viên 20 cố phát sinh hóa đơn cho phòng 101 (CN 1)
            var req = new CreateInvoiceDraftsRequest
            {
                ChiNhanhId = 1,
                Thang = 9,
                Nam = 2026,
                PhongTroIds = new List<int> { 101 }
            };
            var result = await _invoiceIssuanceService.CreateDraftsAsync(req, actorId: 20);

            Assert.False(result.Success);
            Assert.Contains("quyền", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task PhatSinhHoaDon_Rejects_WhenRoomsBelongToDifferentBranchThanRequested()
        {
            // Gửi chi nhánh 2 nhưng danh sách phòng lại là phòng 101 (CN 1)
            var req = new CreateInvoiceDraftsRequest
            {
                ChiNhanhId = 2,
                Thang = 9,
                Nam = 2026,
                PhongTroIds = new List<int> { 101 }
            };
            var result = await _invoiceIssuanceService.CreateDraftsAsync(req, actorId: 20);

            Assert.False(result.Success);
            Assert.Contains("khớp", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task PhatSinhHoaDon_Rejects_WhenRoomDoesNotExist()
        {
            var req = new CreateInvoiceDraftsRequest
            {
                ChiNhanhId = 1,
                Thang = 9,
                Nam = 2026,
                PhongTroIds = new List<int> { 9999 }
            };
            var result = await _invoiceIssuanceService.CreateDraftsAsync(req, actorId: 1);

            Assert.False(result.Success);
            Assert.Contains("không tồn tại", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task UpdateHoaDon_Rejects_WhenStaffNotAssignedToInvoiceBranch()
        {
            // Hóa đơn 1 thuộc CN 1. Nhân viên 20 (chỉ thuộc CN 2) cố sửa.
            var req = new UpdateHoaDonReq { ChiTiets = new List<ChiTietHoaDonUpdateReq>() };
            var result = await _hoaDonService.UpdateHoaDonAsync(hoaDonId: 1, req, actorId: 20);

            Assert.False(result.IsSuccess);
            Assert.Contains("không có quyền", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task UpdateHoaDon_Allows_WhenStaffAssignedToInvoiceBranch_And_InvoiceIsNhap()
        {
            // Hóa đơn ở trạng thái Nháp
            _hoaDonStore.Invoices[1].TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.Nhap;
            var req = new UpdateHoaDonReq { ChiTiets = new List<ChiTietHoaDonUpdateReq>() };
            var result = await _hoaDonService.UpdateHoaDonAsync(hoaDonId: 1, req, actorId: 10);

            Assert.True(result.IsSuccess);
        }

        [Theory]
        [InlineData(TrangThaiPhatHanhHoaDon.ChoDuyet)]
        [InlineData(TrangThaiPhatHanhHoaDon.DaChot)]
        [InlineData(TrangThaiPhatHanhHoaDon.DaGui)]
        [InlineData(TrangThaiPhatHanhHoaDon.DaHuy)]
        public async Task UpdateHoaDon_Rejects_WhenInvoiceIsNotNhap(TrangThaiPhatHanhHoaDon nonDraftStatus)
        {
            var invoice = _hoaDonStore.Invoices[1];
            invoice.TrangThaiPhatHanh = nonDraftStatus;
            invoice.TongTien = 1_000_000m;
            invoice.ChiTietHoaDonDichVus = new List<ChiTietHoaDon>
            {
                new ChiTietHoaDon { TenDichVu = "Tiền phòng", TongTien = 1_000_000m, DonGia = 1_000_000m, SoLuong = 1 }
            };

            var initialTongTien = invoice.TongTien;
            var initialDetailsCount = invoice.ChiTietHoaDonDichVus.Count;

            var req = new UpdateHoaDonReq
            {
                ChiTiets = new List<ChiTietHoaDonUpdateReq>
                {
                    new ChiTietHoaDonUpdateReq { TenDichVu = "Tiền phòng sửa", DonGia = 500_000m, SoLuong = 1 }
                }
            };

            var result = await _hoaDonService.UpdateHoaDonAsync(hoaDonId: 1, req, actorId: 10);

            Assert.False(result.IsSuccess);
            Assert.Contains("Chỉ hóa đơn nháp mới được chỉnh sửa", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(initialTongTien, invoice.TongTien);
            Assert.Equal(initialDetailsCount, invoice.ChiTietHoaDonDichVus.Count);
        }

        [Fact]
        public async Task DeleteHoaDon_Rejects_WhenStaffNotAssignedToInvoiceBranch()
        {
            // Nhân viên 20 cố hủy hóa đơn 1
            var result = await _hoaDonService.DeleteHoaDonAsync(id: 1, actorId: 20, lyDo: "Lý do hủy kiểm thử");

            Assert.False(result.IsSuccess);
            Assert.Contains("không có quyền", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task DeleteHoaDon_Allows_WhenStaffAssignedToInvoiceBranch()
        {
            // Nhân viên 10 hủy hóa đơn 1 (đã chốt)
            var result = await _hoaDonService.DeleteHoaDonAsync(id: 1, actorId: 10, lyDo: "Lý do hủy kiểm thử");

            Assert.True(result.IsSuccess);
            Assert.Equal(TrangThaiPhatHanhHoaDon.DaHuy, _hoaDonStore.Invoices[1].TrangThaiPhatHanh);
            Assert.True(_hoaDonStore.Invoices[1].IsDeleted);
        }

        [Fact]
        public async Task CheckInvoicePermission_Rejects_SendAction_WhenStaffNotAssignedToBranch()
        {
            var result = await _hoaDonService.CheckInvoicePermissionAsync(hoaDonId: 1, actorId: 20, actionCode: EmployeeActionCodes.InvoiceSend);

            Assert.False(result.IsSuccess);
            Assert.Contains("không có quyền", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task CheckInvoicePermission_Allows_SendAction_WhenStaffAssignedToBranch()
        {
            var result = await _hoaDonService.CheckInvoicePermissionAsync(hoaDonId: 1, actorId: 10, actionCode: EmployeeActionCodes.InvoiceSend);

            Assert.True(result.IsSuccess);
        }

        [Fact]
        public async Task Admin_CanPerform_AnyInvoiceAction_AtAnyBranch()
        {
            // Admin kiểm tra gửi hóa đơn 1 (CN 1) và hóa đơn 2 (CN 2)
            var check1 = await _hoaDonService.CheckInvoicePermissionAsync(hoaDonId: 1, actorId: 1, actionCode: EmployeeActionCodes.InvoiceSend);
            var check2 = await _hoaDonService.CheckInvoicePermissionAsync(hoaDonId: 2, actorId: 1, actionCode: EmployeeActionCodes.InvoiceSend);

            Assert.True(check1.IsSuccess);
            Assert.True(check2.IsSuccess);
        }

        [Fact]
        public async Task GetHoaDonById_ReturnsNull_WhenStaffNotAssignedToInvoiceBranch()
        {
            _hoaDonStore.InvoiceDetails[1] = new HoaDonChiTietRes { HoaDonId = 1, MaHoaDon = "HD-1", Email = "tenant@test.com" };

            // Nhân viên 20 (chỉ thuộc CN 2) cố đọc hóa đơn 1 (thuộc CN 1)
            var detail = await _hoaDonService.GetEmployeeInvoiceDetailAsync(id: 1, actorId: 20);

            Assert.Null(detail);
        }

        [Fact]
        public async Task GetHoaDonById_ReturnsDetail_WhenStaffAssignedToInvoiceBranch()
        {
            _hoaDonStore.InvoiceDetails[1] = new HoaDonChiTietRes { HoaDonId = 1, MaHoaDon = "HD-1", Email = "tenant@test.com" };

            // Nhân viên 10 (thuộc CN 1) đọc hóa đơn 1 (thuộc CN 1)
            var detail = await _hoaDonService.GetEmployeeInvoiceDetailAsync(id: 1, actorId: 10);

            Assert.NotNull(detail);
            Assert.Equal("HD-1", detail.MaHoaDon);
        }

        [Fact]
        public async Task ExportPdf_ReturnsNull_WhenStaffNotAssignedToInvoiceBranch()
        {
            _hoaDonStore.InvoiceDetails[1] = new HoaDonChiTietRes { HoaDonId = 1, MaHoaDon = "HD-1" };

            var pdf = await _hoaDonService.ExportEmployeePdfAsync(hoaDonId: 1, actorId: 20);

            Assert.Null(pdf);
        }

        [Fact]
        public async Task ExportExcel_ReturnsNull_WhenStaffNotAssignedToInvoiceBranch()
        {
            _hoaDonStore.InvoiceDetails[1] = new HoaDonChiTietRes { HoaDonId = 1, MaHoaDon = "HD-1" };

            var excel = await _hoaDonService.ExportEmployeeExcelAsync(hoaDonId: 1, actorId: 20);

            Assert.Null(excel);
        }

        [Fact]
        public async Task PreviewPhatSinh_RejectsBeforeQuery_WhenStaffNotAssignedToBranch()
        {
            var result = await _hoaDonService.PreviewPhatSinhHoaDonAsync(1, 9, 2026, actorId: 20);

            Assert.Empty(result);
            Assert.Equal(0, _hoaDonStore.GetPhongTrosCalls);
        }

        [Fact]
        public async Task EmployeeInvoiceList_FailsClosed_WhenActorIsMissingOrInactive()
        {
            var result = await _hoaDonService.GetEmployeeInvoiceListAsync(
                new DataTableRequest { Draw = 7 },
                chiNhanhId: 0,
                thang: 9,
                nam: 2026,
                trangThai: -1,
                actorId: 999);

            Assert.Empty(result.data);
            Assert.Equal(7, result.draw);
            Assert.Equal(0, _hoaDonStore.DataTableCalls);
        }

        [Fact]
        public async Task EmployeeUnpaidList_RejectsBeforeQuery_WhenStaffNotAssignedToBranch()
        {
            var result = await _hoaDonService.GetEmployeeUnpaidInvoicesAsync(1, 9, 2026, actorId: 20);

            Assert.Empty(result);
            Assert.Equal(0, _hoaDonStore.UnpaidInvoicesCalls);
        }

    }
}
