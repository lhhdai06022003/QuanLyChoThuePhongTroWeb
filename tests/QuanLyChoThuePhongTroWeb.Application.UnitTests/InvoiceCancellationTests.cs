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
            public int BeginTransactionCalls { get; private set; }

            public Task<IApplicationTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
            {
                BeginTransactionCalls++;
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
            public Func<bool>? IsInsideTransaction { get; set; }
            public List<bool> LockedReadInsideTransaction { get; } = new();
            public Action<HoaDon>? OnLockedRead { get; set; }
            public List<LichSuThanhToan> AddedPayments { get; } = new();

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
                LockedReadInsideTransaction.Add(IsInsideTransaction?.Invoke() ?? false);
                Invoices.TryGetValue(id, out var hd);
                if (hd != null)
                {
                    OnLockedRead?.Invoke(hd);
                }
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
            public Task AddLichSuThanhToanAsync(LichSuThanhToan lichSu, CancellationToken cancellationToken = default)
            {
                AddedPayments.Add(lichSu);
                return Task.CompletedTask;
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

            _hoaDonStore.IsInsideTransaction = () => _unitOfWork.BeginTransactionCalls > 0
                && !_unitOfWork.CurrentTransaction.Committed
                && !_unitOfWork.CurrentTransaction.RolledBack;

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

        private static HoaDon CreateDraftWithOneLine()
        {
            return new HoaDon
            {
                HoaDonId = 1,
                TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.Nhap,
                TrangThaiHoaDon = TrangThaiHoaDon.ChuaThanhToan,
                TongTien = 500000m,
                IsDeleted = false,
                ChiTietHoaDonDichVus = new List<ChiTietHoaDon>
                {
                    new ChiTietHoaDon { ChiTietHoaDonId = 1, HoaDonId = 1, TenDichVu = "Tiền phòng", DonGia = 500000m, SoLuong = 1, TongTien = 500000m }
                }
            };
        }

        private static UpdateHoaDonReq CreateUpdateReq()
        {
            return new UpdateHoaDonReq
            {
                ChiTiets = new List<ChiTietHoaDonUpdateReq>
                {
                    new ChiTietHoaDonUpdateReq { TenDichVu = "Tiền phòng", DonGia = 600000m, SoLuong = 1 }
                }
            };
        }

        private static HoaDon CreateDaGuiInvoice(TrangThaiHoaDon trangThai = TrangThaiHoaDon.ChuaThanhToan)
        {
            return new HoaDon
            {
                HoaDonId = 1,
                TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaGui,
                TrangThaiHoaDon = trangThai,
                TongTien = 1000000m,
                IsDeleted = false
            };
        }

        [Fact]
        public async Task UpdateHoaDon_ReadsInvoiceWithLockInsideTransaction_AndCommits()
        {
            _hoaDonStore.Invoices[1] = CreateDraftWithOneLine();

            var result = await _hoaDonService.UpdateHoaDonAsync(1, CreateUpdateReq(), actorId: 10);

            Assert.True(result.IsSuccess);
            Assert.Equal(new[] { true }, _hoaDonStore.LockedReadInsideTransaction);
            Assert.True(_unitOfWork.CurrentTransaction.Committed);
        }

        [Fact]
        public async Task UpdateHoaDon_WhenSubmittedBeforeLockAcquired_RejectsAndRollsBack()
        {
            var invoice = CreateDraftWithOneLine();
            _hoaDonStore.Invoices[1] = invoice;
            _hoaDonStore.OnLockedRead = hd => hd.TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.ChoDuyet;

            var result = await _hoaDonService.UpdateHoaDonAsync(1, CreateUpdateReq(), actorId: 10);

            Assert.False(result.IsSuccess);
            Assert.Equal("Chỉ hóa đơn nháp mới được chỉnh sửa. Hóa đơn chờ duyệt phải được Admin trả lại trước khi sửa.", result.ErrorMessage);
            Assert.Equal(500000m, invoice.TongTien);
            Assert.Single(invoice.ChiTietHoaDonDichVus);
            Assert.True(_unitOfWork.CurrentTransaction.RolledBack);
        }

        [Theory]
        [InlineData("", 100, 1, "Tên dịch vụ không được để trống.")]
        [InlineData("   ", 100, 1, "Tên dịch vụ không được để trống.")]
        [InlineData("Điện", -1, 1, "Đơn giá và số lượng phải lớn hơn hoặc bằng 0.")]
        [InlineData("Điện", 100, -1, "Đơn giá và số lượng phải lớn hơn hoặc bằng 0.")]
        public async Task UpdateHoaDon_InvalidLine_KeepsMessage_RollsBack_NoSave(string ten, int donGia, int soLuong, string expected)
        {
            var invoice = CreateDraftWithOneLine();
            _hoaDonStore.Invoices[1] = invoice;
            var req = new UpdateHoaDonReq
            {
                ChiTiets = new List<ChiTietHoaDonUpdateReq>
                {
                    new ChiTietHoaDonUpdateReq { TenDichVu = ten, DonGia = donGia, SoLuong = soLuong }
                }
            };

            var result = await _hoaDonService.UpdateHoaDonAsync(1, req, actorId: 10);

            Assert.False(result.IsSuccess);
            Assert.Equal(expected, result.ErrorMessage);
            Assert.Equal(500000m, invoice.TongTien);
            Assert.Single(invoice.ChiTietHoaDonDichVus);
            Assert.True(_unitOfWork.CurrentTransaction.RolledBack);
            Assert.False(_unitOfWork.CurrentTransaction.Committed);
        }

        [Fact]
        public async Task UpdateHoaDon_WhenAlreadyPaid_KeepsMessage_RollsBack()
        {
            var invoice = CreateDraftWithOneLine();
            invoice.TrangThaiHoaDon = TrangThaiHoaDon.DaThanhToan;
            _hoaDonStore.Invoices[1] = invoice;

            var result = await _hoaDonService.UpdateHoaDonAsync(1, CreateUpdateReq(), actorId: 10);

            Assert.False(result.IsSuccess);
            Assert.Equal("Không thể chỉnh sửa hóa đơn đã được thanh toán.", result.ErrorMessage);
            Assert.Equal(500000m, invoice.TongTien);
            Assert.True(_unitOfWork.CurrentTransaction.RolledBack);
        }

        // Nháp có tiền phòng tính theo ngày: thuê từ 16/09, giá 3.000.000 → 1.600.000 (SoLuong = 1).
        private static HoaDon CreateProratedDraft()
        {
            return new HoaDon
            {
                HoaDonId = 1,
                TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.Nhap,
                TrangThaiHoaDon = TrangThaiHoaDon.ChuaThanhToan,
                TongTien = 1700000m,
                IsDeleted = false,
                ChiTietHoaDonDichVus = new List<ChiTietHoaDon>
                {
                    new ChiTietHoaDon { ChiTietHoaDonId = 1, HoaDonId = 1, TenDichVu = "Tiền phòng", DonGia = 3000000m, SoLuong = 1, TongTien = 1600000m },
                    new ChiTietHoaDon { ChiTietHoaDonId = 2, HoaDonId = 1, TenDichVu = "Rác", DonGia = 50000m, SoLuong = 2, TongTien = 100000m }
                }
            };
        }

        [Fact]
        public async Task UpdateHoaDon_UnchangedProratedLine_KeepsStoredAmount()
        {
            var invoice = CreateProratedDraft();
            _hoaDonStore.Invoices[1] = invoice;
            var req = new UpdateHoaDonReq
            {
                ChiTiets = new List<ChiTietHoaDonUpdateReq>
                {
                    new ChiTietHoaDonUpdateReq { ChiTietHoaDonId = 1, TenDichVu = "Tiền phòng", DonGia = 3000000m, SoLuong = 1 },
                    new ChiTietHoaDonUpdateReq { ChiTietHoaDonId = 2, TenDichVu = "Rác", DonGia = 50000m, SoLuong = 2 }
                }
            };

            var result = await _hoaDonService.UpdateHoaDonAsync(1, req, actorId: 10);

            Assert.True(result.IsSuccess, result.ErrorMessage);
            Assert.Equal(1600000m, invoice.ChiTietHoaDonDichVus.Single(x => x.TenDichVu == "Tiền phòng").TongTien);
            Assert.Equal(1700000m, invoice.TongTien);
        }

        [Fact]
        public async Task UpdateHoaDon_RenamedButSamePriceAndQuantity_KeepsStoredAmount()
        {
            var invoice = CreateProratedDraft();
            _hoaDonStore.Invoices[1] = invoice;
            var req = new UpdateHoaDonReq
            {
                ChiTiets = new List<ChiTietHoaDonUpdateReq>
                {
                    new ChiTietHoaDonUpdateReq { ChiTietHoaDonId = 1, TenDichVu = "Tiền phòng tháng 9", DonGia = 3000000m, SoLuong = 1 },
                    new ChiTietHoaDonUpdateReq { ChiTietHoaDonId = 2, TenDichVu = "Rác", DonGia = 50000m, SoLuong = 2 }
                }
            };

            var result = await _hoaDonService.UpdateHoaDonAsync(1, req, actorId: 10);

            Assert.True(result.IsSuccess, result.ErrorMessage);
            Assert.Equal(1600000m, invoice.ChiTietHoaDonDichVus.Single(x => x.TenDichVu == "Tiền phòng tháng 9").TongTien);
        }

        [Fact]
        public async Task UpdateHoaDon_ChangedLine_IsRecalculated_OthersKept()
        {
            var invoice = CreateProratedDraft();
            _hoaDonStore.Invoices[1] = invoice;
            var req = new UpdateHoaDonReq
            {
                ChiTiets = new List<ChiTietHoaDonUpdateReq>
                {
                    new ChiTietHoaDonUpdateReq { ChiTietHoaDonId = 1, TenDichVu = "Tiền phòng", DonGia = 3000000m, SoLuong = 1 },
                    new ChiTietHoaDonUpdateReq { ChiTietHoaDonId = 2, TenDichVu = "Rác", DonGia = 50000m, SoLuong = 3 },
                    new ChiTietHoaDonUpdateReq { ChiTietHoaDonId = 0, TenDichVu = "Phí khác", DonGia = 20000m, SoLuong = 1 }
                }
            };

            var result = await _hoaDonService.UpdateHoaDonAsync(1, req, actorId: 10);

            Assert.True(result.IsSuccess, result.ErrorMessage);
            Assert.Equal(1600000m, invoice.ChiTietHoaDonDichVus.Single(x => x.TenDichVu == "Tiền phòng").TongTien);
            Assert.Equal(150000m, invoice.ChiTietHoaDonDichVus.Single(x => x.TenDichVu == "Rác").TongTien);
            Assert.Equal(20000m, invoice.ChiTietHoaDonDichVus.Single(x => x.TenDichVu == "Phí khác").TongTien);
            Assert.Equal(1770000m, invoice.TongTien);
        }

        [Fact]
        public async Task UpdateHoaDon_SameLineIdSentTwice_KeepsStoredAmountOnlyOnce()
        {
            var invoice = CreateProratedDraft();
            _hoaDonStore.Invoices[1] = invoice;
            var req = new UpdateHoaDonReq
            {
                ChiTiets = new List<ChiTietHoaDonUpdateReq>
                {
                    new ChiTietHoaDonUpdateReq { ChiTietHoaDonId = 1, TenDichVu = "Tiền phòng", DonGia = 3000000m, SoLuong = 1 },
                    new ChiTietHoaDonUpdateReq { ChiTietHoaDonId = 1, TenDichVu = "Tiền phòng", DonGia = 3000000m, SoLuong = 1 }
                }
            };

            var result = await _hoaDonService.UpdateHoaDonAsync(1, req, actorId: 10);

            Assert.True(result.IsSuccess, result.ErrorMessage);
            Assert.Equal(4600000m, invoice.TongTien);
        }

        [Fact]
        public async Task UpdateHoaDon_LineIdFromAnotherInvoiceOrUnknown_IsRecalculated()
        {
            var invoice = CreateProratedDraft();
            _hoaDonStore.Invoices[1] = invoice;
            var req = new UpdateHoaDonReq
            {
                ChiTiets = new List<ChiTietHoaDonUpdateReq>
                {
                    new ChiTietHoaDonUpdateReq { ChiTietHoaDonId = 999, TenDichVu = "Tiền phòng", DonGia = 3000000m, SoLuong = 1 }
                }
            };

            var result = await _hoaDonService.UpdateHoaDonAsync(1, req, actorId: 10);

            Assert.True(result.IsSuccess, result.ErrorMessage);
            Assert.Equal(3000000m, invoice.TongTien);
        }

        [Fact]
        public async Task ThuTien_ReadsInvoiceWithLockInsideTransaction()
        {
            _hoaDonStore.Invoices[1] = CreateDaGuiInvoice();

            var result = await _hoaDonService.ThuTienAsync(1, (int)PhuongThucThanhToan.TienMat, "Thu tien", nguoiXacNhanId: 10);

            Assert.True(result.IsSuccess);
            Assert.Equal(new[] { true }, _hoaDonStore.LockedReadInsideTransaction);
        }

        [Fact]
        public async Task ThuTien_WhenPaidBeforeLockAcquired_RejectsWithoutPayment()
        {
            _hoaDonStore.Invoices[1] = CreateDaGuiInvoice();
            _hoaDonStore.OnLockedRead = hd => hd.TrangThaiHoaDon = TrangThaiHoaDon.DaThanhToan;

            var result = await _hoaDonService.ThuTienAsync(1, (int)PhuongThucThanhToan.TienMat, "Thu tien", nguoiXacNhanId: 10);

            Assert.False(result.IsSuccess);
            Assert.Equal("Hóa đơn này đã được thanh toán trước đó.", result.ErrorMessage);
            Assert.Empty(_hoaDonStore.AddedPayments);
            Assert.True(_unitOfWork.CurrentTransaction.RolledBack);
        }

        [Fact]
        public async Task DeleteHoaDon_ReadsInvoiceWithLockInsideTransaction()
        {
            _hoaDonStore.Invoices[1] = new HoaDon { HoaDonId = 1, TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaChot, IsDeleted = false };

            var result = await _hoaDonService.DeleteHoaDonAsync(id: 1, actorId: 10, lyDo: "Hủy kiểm thử");

            Assert.True(result.IsSuccess);
            Assert.Equal(new[] { true }, _hoaDonStore.LockedReadInsideTransaction);
        }

        [Fact]
        public async Task DeleteHoaDon_WhenCancelledBeforeLockAcquired_ReturnsNotFound()
        {
            _hoaDonStore.Invoices[1] = new HoaDon { HoaDonId = 1, TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaChot, IsDeleted = false };
            _hoaDonStore.OnLockedRead = hd =>
            {
                hd.IsDeleted = true;
                hd.TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaHuy;
            };

            var result = await _hoaDonService.DeleteHoaDonAsync(id: 1, actorId: 10, lyDo: "Hủy kiểm thử");

            Assert.False(result.IsSuccess);
            Assert.Equal("Không tìm thấy hóa đơn.", result.ErrorMessage);
            Assert.True(_unitOfWork.CurrentTransaction.RolledBack);
        }

        [Fact]
        public async Task ThuTien_Rejects_WhenThanhToanMotPhan_NoPaymentAdded()
        {
            var invoice = CreateDaGuiInvoice(TrangThaiHoaDon.ThanhToanMotPhan);
            _hoaDonStore.Invoices[1] = invoice;

            var result = await _hoaDonService.ThuTienAsync(1, (int)PhuongThucThanhToan.TienMat, "Thu tien", nguoiXacNhanId: 10);

            Assert.False(result.IsSuccess);
            Assert.Equal("Hóa đơn đã được thanh toán một phần. Vui lòng xác nhận phần còn lại qua luồng xác nhận thanh toán.", result.ErrorMessage);
            Assert.Empty(_hoaDonStore.AddedPayments);
            Assert.Equal(TrangThaiHoaDon.ThanhToanMotPhan, invoice.TrangThaiHoaDon);
            Assert.True(_unitOfWork.CurrentTransaction.RolledBack);
        }
    }
}
