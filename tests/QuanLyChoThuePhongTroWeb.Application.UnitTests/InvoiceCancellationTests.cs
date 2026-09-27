using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
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
    public class InvoiceCancellationTests
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
            public bool ThrowOnSave { get; set; }

            public Task<IApplicationTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult<IApplicationTransaction>(CurrentTransaction);
            }

            public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            {
                if (ThrowOnSave)
                {
                    throw new InvalidOperationException("SECRET_DB_ERROR_12345");
                }
                SaveChangesCalls++;
                return Task.FromResult(1);
            }
        }

        private class FakeHoaDonStore : IHoaDonStore
        {
            public Dictionary<int, HoaDon> Invoices { get; set; } = new();
            public Dictionary<int, int> InvoiceBranches { get; set; } = new();
            public Dictionary<int, InvoiceCancellationBlockers> Blockers { get; set; } = new();
            public int UpdateHoaDonCalls { get; private set; }

            public Task<InvoiceCancellationBlockers> GetCancellationBlockersAsync(int hoaDonId, CancellationToken cancellationToken = default)
            {
                if (Blockers.TryGetValue(hoaDonId, out var b))
                {
                    return Task.FromResult(b);
                }
                return Task.FromResult(new InvoiceCancellationBlockers(false, false, false));
            }

            public Task<HoaDon?> GetActiveHoaDonByIdAsync(int id, CancellationToken cancellationToken = default)
            {
                Invoices.TryGetValue(id, out var hd);
                return Task.FromResult(hd);
            }

            public Task<int?> GetHoaDonBranchIdAsync(int hoaDonId, CancellationToken cancellationToken = default)
            {
                if (InvoiceBranches.TryGetValue(hoaDonId, out var bId)) return Task.FromResult<int?>(bId);
                return Task.FromResult<int?>(null);
            }

            public void UpdateHoaDon(HoaDon hoaDon)
            {
                UpdateHoaDonCalls++;
                Invoices[hoaDon.HoaDonId] = hoaDon;
            }

            public Task<IReadOnlyList<HoaDon>> GetInvoicesByMeterReadingIdsAsync(IReadOnlyList<int> meterReadingIds, CancellationToken cancellationToken = default)
                => Task.FromResult<IReadOnlyList<HoaDon>>(new List<HoaDon>());
            public Task<ChiNhanh?> GetChiNhanhByIdAsync(int chiNhanhId, CancellationToken cancellationToken = default) => Task.FromResult<ChiNhanh?>(null);
            public Task<IReadOnlyList<HopDong>> GetValidContractsForBillingAsync(int chiNhanhId, IReadOnlyList<int> roomIds, DateTime startUtc, DateTime endUtc, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<HopDong>>(new List<HopDong>());
            public Task<IReadOnlyList<int>> GetExistingInvoiceContractIdsAsync(IReadOnlyList<int> contractIds, int thang, int nam, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<int>>(new List<int>());
            public Task<IReadOnlyList<DichVuDienNuocCuaPhong>> GetDichVuDienNuocByRoomIdsAsync(IReadOnlyList<int> roomIds, int thang, int nam, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<DichVuDienNuocCuaPhong>>(new List<DichVuDienNuocCuaPhong>());
            public Task<IReadOnlyList<DangKyDichVu>> GetDangKyDichVusForBillingAsync(IReadOnlyList<int> roomIds, DateTime startUtc, DateTime endUtc, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<DangKyDichVu>>(new List<DangKyDichVu>());
            public Task<IReadOnlyList<YeuCauSuCo>> GetBillableSuCosAsync(IReadOnlyList<int> roomIds, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<YeuCauSuCo>>(new List<YeuCauSuCo>());
            public Task<IReadOnlyList<PhongTro>> GetPhongTrosByChiNhanhIdAsync(int chiNhanhId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<PhongTro>>(new List<PhongTro>());
            public Task<HoaDon?> GetHoaDonWithDetailsForUpdateAsync(int id, CancellationToken cancellationToken = default)
            {
                Invoices.TryGetValue(id, out var hd);
                return Task.FromResult(hd);
            }
            public Task<DataTableResponse<HoaDonRes>> GetHoaDonsDataTableAsync(DataTableRequest request, int chiNhanhId, int thang, int nam, int trangThai, IReadOnlyList<int>? allowedBranchIds = null, CancellationToken cancellationToken = default) => Task.FromResult(new DataTableResponse<HoaDonRes>());
            public Task<HoaDonChiTietRes?> GetHoaDonDetailByIdAsync(int id, CancellationToken cancellationToken = default) => Task.FromResult<HoaDonChiTietRes?>(null);
            public Task<IReadOnlyList<HoaDonRes>> GetUnpaidInvoicesAsync(int chiNhanhId, int thang, int nam, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<HoaDonRes>>(new List<HoaDonRes>());
            public Task<IReadOnlyList<int>> GetContractIdsByTenantIdAsync(int nguoiThueId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<int>>(new List<int>());
            public Task<IReadOnlyList<HoaDonRes>> GetInvoicesByContractIdsAsync(IReadOnlyList<int> contractIds, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<HoaDonRes>>(new List<HoaDonRes>());
            public Task<bool> CheckHoaDonOwnershipAsync(int hoaDonId, int nguoiThueId, CancellationToken cancellationToken = default) => Task.FromResult(false);
            public Task<IReadOnlyList<HoaDon>> GetOverdueInvoicesAsync(DateTime thresholdUtc, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<HoaDon>>(new List<HoaDon>());
            public Task<IReadOnlyDictionary<int, int>> GetRoomBranchIdsAsync(IReadOnlyList<int> roomIds, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyDictionary<int, int>>(new Dictionary<int, int>());
            public Task AddInvoicesAsync(IEnumerable<HoaDon> invoices, CancellationToken cancellationToken = default) => Task.CompletedTask;
            public void UpdateSuCos(IEnumerable<YeuCauSuCo> suCos) { }
            public void RemoveChiTietHoaDons(IEnumerable<ChiTietHoaDon> chiTiets) { }
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
            public (decimal SoTien, string DienGiai, int SoNgayO) TinhTienPhong(decimal giaThue, DateTime batDau, DateTime? ketThuc, int thang, int nam) => (giaThue, "", 30);
            public (decimal SoLuong, decimal SoTien, string DienGiai) TinhTienDienNuoc(decimal chiSoMoi, decimal chiSoCu, decimal donGia, string tenDichVu, int soNgayO, int tongNgayTrongThang) => (0, 0, "");
            public (decimal SoTien, string DienGiai) TinhTienDichVuCoDinh(decimal giaDv, decimal soLuong, string tenDichVu, DateTime batDau, DateTime? ketThuc, int thang, int nam, DateTime? contractStart = null, DateTime? contractEnd = null) => (0, "");
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
            public Task<(bool IsSuccess, string ErrorMessage)> SendInvoiceEmailAsync(string toEmail, HoaDonChiTietRes hoaDon, byte[] pdfBytes)
                => Task.FromResult((true, string.Empty));
            public Task<(bool IsSuccess, string ErrorMessage)> SendContractExpiryAlertAsync(string toEmail, string tenNguoiNhan, QuanLyChoThuePhongTroWeb.Application.Features.Emails.DTOs.ContractExpiryAlertData alertData)
                => Task.FromResult((true, string.Empty));
        }

        private readonly FakeHoaDonStore _hoaDonStore;
        private readonly FakeUnitOfWork _unitOfWork;
        private readonly FakeEmployeeBranchStore _employeeStore;
        private readonly EmployeeAccessService _accessService;
        private readonly HoaDonService _hoaDonService;

        public InvoiceCancellationTests()
        {
            _hoaDonStore = new FakeHoaDonStore();
            _unitOfWork = new FakeUnitOfWork();
            _employeeStore = new FakeEmployeeBranchStore();
            _accessService = new EmployeeAccessService(_employeeStore);

            _hoaDonService = new HoaDonService(
                _hoaDonStore,
                _unitOfWork,
                new VietQrSettings(),
                NullLogger<HoaDonService>.Instance,
                new FakeCalculatorService(),
                new FakeDocumentExporter(),
                new FakeVietQRService(),
                _accessService,
                new FakeEmailService());

            // Nhân viên 10 thuộc CN 1
            _employeeStore.Actors[10] = new EmployeeBranchActorDto
            {
                NguoiDungId = 10,
                Role = Role.NhanVien,
                IsActive = true,
                ActiveBranchIds = new List<int> { 1 }
            };

            // Hóa đơn 1 thuộc CN 1
            _hoaDonStore.InvoiceBranches[1] = 1;
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public async Task DeleteHoaDon_WithoutReason_RejectsCancellation(string? emptyReason)
        {
            _hoaDonStore.Invoices[1] = new HoaDon
            {
                HoaDonId = 1,
                TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaChot,
                IsDeleted = false
            };

            var result = await _hoaDonService.DeleteHoaDonAsync(id: 1, actorId: 10, lyDo: emptyReason!);

            Assert.False(result.IsSuccess);
            Assert.Contains("lý do", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
            Assert.False(_hoaDonStore.Invoices[1].IsDeleted);
            Assert.Equal(TrangThaiPhatHanhHoaDon.DaChot, _hoaDonStore.Invoices[1].TrangThaiPhatHanh);
        }

        [Theory]
        [InlineData(TrangThaiPhatHanhHoaDon.Nhap)]
        [InlineData(TrangThaiPhatHanhHoaDon.ChoDuyet)]
        public async Task DeleteHoaDon_DraftOrPendingApproval_RejectsCancellation(TrangThaiPhatHanhHoaDon invalidStatus)
        {
            _hoaDonStore.Invoices[1] = new HoaDon
            {
                HoaDonId = 1,
                TrangThaiPhatHanh = invalidStatus,
                IsDeleted = false
            };

            var result = await _hoaDonService.DeleteHoaDonAsync(id: 1, actorId: 10, lyDo: "Lý do hủy hợp lệ");

            Assert.False(result.IsSuccess);
            Assert.Contains("chỉ hóa đơn đã chốt hoặc đã gửi", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
            Assert.False(_hoaDonStore.Invoices[1].IsDeleted);
            Assert.Equal(invalidStatus, _hoaDonStore.Invoices[1].TrangThaiPhatHanh);
        }

        [Fact]
        public async Task DeleteHoaDon_WithPayment_RejectsCancellation()
        {
            _hoaDonStore.Invoices[1] = new HoaDon
            {
                HoaDonId = 1,
                TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaChot,
                IsDeleted = false
            };
            _hoaDonStore.Blockers[1] = new InvoiceCancellationBlockers(HasPayment: true, HasPendingRequest: false, HasPendingProof: false);

            var result = await _hoaDonService.DeleteHoaDonAsync(id: 1, actorId: 10, lyDo: "Lý do hủy");

            Assert.False(result.IsSuccess);
            Assert.Contains("khoản thanh toán", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
            Assert.False(_hoaDonStore.Invoices[1].IsDeleted);
            Assert.True(_unitOfWork.CurrentTransaction.RolledBack);
        }

        [Fact]
        public async Task DeleteHoaDon_WithPendingPaymentRequest_RejectsCancellation()
        {
            _hoaDonStore.Invoices[1] = new HoaDon
            {
                HoaDonId = 1,
                TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaGui,
                IsDeleted = false
            };
            _hoaDonStore.Blockers[1] = new InvoiceCancellationBlockers(HasPayment: false, HasPendingRequest: true, HasPendingProof: false);

            var result = await _hoaDonService.DeleteHoaDonAsync(id: 1, actorId: 10, lyDo: "Lý do hủy");

            Assert.False(result.IsSuccess);
            Assert.Contains("chờ xử lý", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
            Assert.False(_hoaDonStore.Invoices[1].IsDeleted);
            Assert.True(_unitOfWork.CurrentTransaction.RolledBack);
        }

        [Fact]
        public async Task DeleteHoaDon_WithPendingProof_RejectsCancellation()
        {
            _hoaDonStore.Invoices[1] = new HoaDon
            {
                HoaDonId = 1,
                TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaGui,
                IsDeleted = false
            };
            _hoaDonStore.Blockers[1] = new InvoiceCancellationBlockers(HasPayment: false, HasPendingRequest: false, HasPendingProof: true);

            var result = await _hoaDonService.DeleteHoaDonAsync(id: 1, actorId: 10, lyDo: "Lý do hủy");

            Assert.False(result.IsSuccess);
            Assert.Contains("chờ xử lý", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
            Assert.False(_hoaDonStore.Invoices[1].IsDeleted);
            Assert.True(_unitOfWork.CurrentTransaction.RolledBack);
        }

        [Fact]
        public async Task DeleteHoaDon_CleanDaChot_CancelsSuccessfully()
        {
            var invoice = new HoaDon
            {
                HoaDonId = 1,
                TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaChot,
                IsDeleted = false
            };
            _hoaDonStore.Invoices[1] = invoice;
            _hoaDonStore.Blockers[1] = new InvoiceCancellationBlockers(HasPayment: false, HasPendingRequest: false, HasPendingProof: false);

            var result = await _hoaDonService.DeleteHoaDonAsync(id: 1, actorId: 10, lyDo: "Hủy do nhập sai chỉ số");

            Assert.True(result.IsSuccess);
            Assert.True(invoice.IsDeleted);
            Assert.Equal(TrangThaiPhatHanhHoaDon.DaHuy, invoice.TrangThaiPhatHanh);
            Assert.Single(invoice.LichSuTrangThaiHoaDons);
            Assert.Equal("Hủy do nhập sai chỉ số", invoice.LichSuTrangThaiHoaDons.First().LyDo);
            Assert.True(_unitOfWork.CurrentTransaction.Committed);
        }

        [Fact]
        public async Task DeleteHoaDon_CleanDaGui_CancelsSuccessfully()
        {
            var invoice = new HoaDon
            {
                HoaDonId = 1,
                TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaGui,
                IsDeleted = false
            };
            _hoaDonStore.Invoices[1] = invoice;
            _hoaDonStore.Blockers[1] = new InvoiceCancellationBlockers(HasPayment: false, HasPendingRequest: false, HasPendingProof: false);

            var result = await _hoaDonService.DeleteHoaDonAsync(id: 1, actorId: 10, lyDo: "Hủy để phát hành lại");

            Assert.True(result.IsSuccess);
            Assert.True(invoice.IsDeleted);
            Assert.Equal(TrangThaiPhatHanhHoaDon.DaHuy, invoice.TrangThaiPhatHanh);
            Assert.Single(invoice.LichSuTrangThaiHoaDons);
            Assert.Equal("Hủy để phát hành lại", invoice.LichSuTrangThaiHoaDons.First().LyDo);
            Assert.True(_unitOfWork.CurrentTransaction.Committed);
        }

        [Fact]
        public async Task DeleteHoaDon_AlreadyCancelled_RejectsCancellation()
        {
            var invoice = new HoaDon
            {
                HoaDonId = 1,
                TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaHuy,
                IsDeleted = true
            };
            _hoaDonStore.Invoices[1] = invoice;

            var result = await _hoaDonService.DeleteHoaDonAsync(id: 1, actorId: 10, lyDo: "Hủy tiếp");

            Assert.False(result.IsSuccess);
        }

        [Fact]
        public async Task DeleteHoaDon_UsesRowLock_AndDoesNotLeakExceptionMessage()
        {
            var invoice = new HoaDon
            {
                HoaDonId = 1,
                TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaChot,
                IsDeleted = false
            };
            _hoaDonStore.Invoices[1] = invoice;
            _unitOfWork.ThrowOnSave = true;

            var result = await _hoaDonService.DeleteHoaDonAsync(id: 1, actorId: 10, lyDo: "Lý do hủy hợp lệ");

            Assert.False(result.IsSuccess);
            Assert.DoesNotContain("SECRET_DB_ERROR_12345", result.ErrorMessage);
            Assert.Equal("Lỗi hệ thống khi hủy hóa đơn.", result.ErrorMessage);
            Assert.True(_unitOfWork.CurrentTransaction.RolledBack);
        }

        [Fact]
        public async Task DeleteHoaDon_DoesNotCallUpdateHoaDon()
        {
            var invoice = new HoaDon
            {
                HoaDonId = 1,
                TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaChot,
                IsDeleted = false
            };
            _hoaDonStore.Invoices[1] = invoice;

            var result = await _hoaDonService.DeleteHoaDonAsync(id: 1, actorId: 10, lyDo: "Lý do hủy hợp lệ");

            Assert.True(result.IsSuccess);
            Assert.Equal(0, _hoaDonStore.UpdateHoaDonCalls);
        }

        [Theory]
        [InlineData(TrangThaiPhatHanhHoaDon.Nhap)]
        [InlineData(TrangThaiPhatHanhHoaDon.ChoDuyet)]
        [InlineData(TrangThaiPhatHanhHoaDon.DaChot)]
        public async Task ThuTien_Fails_WhenNotDaGui(TrangThaiPhatHanhHoaDon status)
        {
            _hoaDonStore.Invoices[1] = new HoaDon
            {
                HoaDonId = 1,
                TrangThaiPhatHanh = status,
                TrangThaiHoaDon = TrangThaiHoaDon.ChuaThanhToan,
                TongTien = 1000000m,
                IsDeleted = false
            };

            var result = await _hoaDonService.ThuTienAsync(1, (int)PhuongThucThanhToan.TienMat, "Thu tien", nguoiXacNhanId: 10);

            Assert.False(result.IsSuccess);
            Assert.Contains("đã gửi", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task ThuTien_Fails_WhenCancelledOrDeleted()
        {
            _hoaDonStore.Invoices[1] = new HoaDon
            {
                HoaDonId = 1,
                TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaHuy,
                TrangThaiHoaDon = TrangThaiHoaDon.ChuaThanhToan,
                TongTien = 1000000m,
                IsDeleted = true
            };

            var result = await _hoaDonService.ThuTienAsync(1, (int)PhuongThucThanhToan.TienMat, "Thu tien", nguoiXacNhanId: 10);

            Assert.False(result.IsSuccess);
            Assert.Contains("Không tìm thấy", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task ThuTien_Fails_WhenStaffOfOtherBranch()
        {
            _hoaDonStore.Invoices[1] = new HoaDon
            {
                HoaDonId = 1,
                TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaGui,
                TrangThaiHoaDon = TrangThaiHoaDon.ChuaThanhToan,
                TongTien = 1000000m,
                IsDeleted = false
            };

            _employeeStore.Actors[20] = new EmployeeBranchActorDto
            {
                NguoiDungId = 20,
                Role = Role.NhanVien,
                IsActive = true,
                ActiveBranchIds = new List<int> { 2 }
            };

            var result = await _hoaDonService.ThuTienAsync(1, (int)PhuongThucThanhToan.TienMat, "Thu tien", nguoiXacNhanId: 20);

            Assert.False(result.IsSuccess);
            Assert.Contains("không có quyền", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task ThuTien_Succeeds_ForDaGui_AssignedStaff()
        {
            var invoice = new HoaDon
            {
                HoaDonId = 1,
                TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaGui,
                TrangThaiHoaDon = TrangThaiHoaDon.ChuaThanhToan,
                TongTien = 1000000m,
                IsDeleted = false
            };
            _hoaDonStore.Invoices[1] = invoice;

            var result = await _hoaDonService.ThuTienAsync(1, (int)PhuongThucThanhToan.TienMat, "Thu tien mat", nguoiXacNhanId: 10);

            Assert.True(result.IsSuccess);
            Assert.Equal(TrangThaiHoaDon.DaThanhToan, invoice.TrangThaiHoaDon);
            Assert.Equal(0, _hoaDonStore.UpdateHoaDonCalls);
            Assert.True(_unitOfWork.CurrentTransaction.Committed);
        }
    }
}
