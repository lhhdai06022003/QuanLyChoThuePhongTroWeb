using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Notifications;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Security;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Services;
using QuanLyChoThuePhongTroWeb.Application.Common.Configurations;
using QuanLyChoThuePhongTroWeb.Application.Common.Security;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Services;
using QuanLyChoThuePhongTroWeb.Application.UnitTests.Fakes;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    public class InvoicePublicationTests
    {
        private class FakeApplicationTransaction : IApplicationTransaction
        {
            public bool Committed { get; private set; }
            public bool RolledBack { get; private set; }
            public int RollbackCount { get; private set; }
            public int DisposeCount { get; private set; }
            public Exception? RollbackException { get; set; }
            private readonly List<string> _log;
            public FakeApplicationTransaction(List<string> log) => _log = log;

            public Task CommitAsync(CancellationToken cancellationToken = default)
            {
                Committed = true;
                _log.Add("commit");
                return Task.CompletedTask;
            }

            public Task RollbackAsync(CancellationToken cancellationToken = default)
            {
                RollbackCount++;
                RolledBack = true;
                _log.Add("rollback");
                if (RollbackException != null)
                {
                    throw RollbackException;
                }
                return Task.CompletedTask;
            }

            public void Dispose() { DisposeCount++; }
            public ValueTask DisposeAsync()
            {
                DisposeCount++;
                return ValueTask.CompletedTask;
            }
        }

        private class FakeUnitOfWork : IUnitOfWork
        {
            public List<string> EventLog { get; } = new();
            public int BeginTransactionCount { get; private set; }
            public int SaveChangesCount { get; private set; }
            public int? ThrowOnSaveNumber { get; set; }
            public int? ThrowOnBeginNumber { get; set; }
            public HashSet<int> RollbackThrowsForTransactionNumbers { get; } = new();
            public List<FakeApplicationTransaction> Transactions { get; } = new();

            public Task<IApplicationTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
            {
                BeginTransactionCount++;
                if (ThrowOnBeginNumber.HasValue && BeginTransactionCount == ThrowOnBeginNumber.Value)
                {
                    throw new InvalidOperationException("DB failure on begin #" + BeginTransactionCount);
                }

                var tx = new FakeApplicationTransaction(EventLog);
                Transactions.Add(tx);
                if (RollbackThrowsForTransactionNumbers.Contains(Transactions.Count))
                {
                    tx.RollbackException = new InvalidOperationException("Rollback failure for tx #" + Transactions.Count);
                }
                return Task.FromResult<IApplicationTransaction>(tx);
            }

            public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            {
                SaveChangesCount++;
                if (ThrowOnSaveNumber.HasValue && SaveChangesCount == ThrowOnSaveNumber.Value)
                {
                    throw new InvalidOperationException("DB failure on save #" + SaveChangesCount);
                }
                return Task.FromResult(1);
            }
        }

        private class FakeEmployeeAccessService : IEmployeeAccessService
        {
            public HashSet<(int ActorId, int BranchId, string Action)> Permissions { get; } = new();
            public List<(int ActorId, int BranchId, string Action)> Calls { get; } = new();

            public Task<bool> CanPerformAsync(int actorId, int chiNhanhId, string action, CancellationToken cancellationToken = default)
            {
                Calls.Add((actorId, chiNhanhId, action));
                return Task.FromResult(Permissions.Contains((actorId, chiNhanhId, action)));
            }

            public Task<EmployeeAccessScope?> GetScopeAsync(int actorId, CancellationToken cancellationToken = default)
                => Task.FromResult<EmployeeAccessScope?>(null);
        }

        private class FakeEmailService : IEmailService
        {
            public int SendCount { get; private set; }
            public List<string> SentToAddresses { get; } = new();
            public (bool IsSuccess, string ErrorMessage) Result { get; set; } = (true, string.Empty);
            public Exception? ExceptionToThrow { get; set; }
            public List<string> EventLog { get; set; } = new();

            public Task<(bool IsSuccess, string ErrorMessage)> SendInvoiceEmailAsync(string toEmail, HoaDonChiTietRes hoaDon, byte[] pdfBytes)
            {
                SendCount++;
                SentToAddresses.Add(toEmail);
                EventLog.Add("email");
                if (ExceptionToThrow != null) throw ExceptionToThrow;
                return Task.FromResult(Result);
            }

            public Task<(bool IsSuccess, string ErrorMessage)> SendContractExpiryAlertAsync(string toEmail, string tenNguoiNhan, QuanLyChoThuePhongTroWeb.Application.Features.Emails.DTOs.ContractExpiryAlertData alertData)
                => Task.FromResult((true, string.Empty));
        }

        private class FakeDocumentExporter : IInvoiceDocumentExporter
        {
            public byte[] PdfBytes { get; set; } = new byte[] { 1, 2, 3 };
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
            public byte[] GenerateQRCodePNGBytes(string qrString) => new byte[] { 9 };
        }

        private class FakeThongBaoNotifier : IThongBaoNotifier
        {
            public List<object> SentPayloads { get; } = new();
            public Exception? ExceptionToThrow { get; set; }
            public List<string> EventLog { get; set; } = new();

            public Task SendToUserAsync(int nguoiDungId, object payload, CancellationToken cancellationToken = default)
            {
                EventLog.Add("notifier");
                SentPayloads.Add(payload);
                if (ExceptionToThrow != null) throw ExceptionToThrow;
                return Task.CompletedTask;
            }

            public Task SendToRoleGroupAsync(string roleName, object payload, CancellationToken cancellationToken = default) => Task.CompletedTask;
        }

        private class CapturingLogger : ILogger<InvoicePublicationService>
        {
            public List<string> Messages { get; } = new();
            public List<(LogLevel Level, string Message)> Entries { get; } = new();

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                var msg = formatter(state, exception);
                Messages.Add(msg);
                Entries.Add((logLevel, msg));
            }
        }

        private readonly FakeInvoicePublicationStore _store;
        private readonly FakeHoaDonStore _hoaDonStore;
        private readonly FakeUnitOfWork _unitOfWork;
        private readonly FakeEmployeeAccessService _accessService;
        private readonly FakeEmailService _emailService;
        private readonly FakeDocumentExporter _documentExporter;
        private readonly FakeVietQRService _vietQRService;
        private readonly FakeThongBaoNotifier _notifier;
        private readonly CapturingLogger _logger;
        private readonly InvoiceIssuanceOptions _options;
        private readonly InvoicePublicationService _service;

        public InvoicePublicationTests()
        {
            _store = new FakeInvoicePublicationStore();
            _hoaDonStore = new FakeHoaDonStore();
            _unitOfWork = new FakeUnitOfWork();
            _accessService = new FakeEmployeeAccessService();
            _emailService = new FakeEmailService { EventLog = _unitOfWork.EventLog };
            _documentExporter = new FakeDocumentExporter();
            _vietQRService = new FakeVietQRService();
            _notifier = new FakeThongBaoNotifier { EventLog = _unitOfWork.EventLog };
            _logger = new CapturingLogger();
            _options = new InvoiceIssuanceOptions { SoNgayHanThanhToan = 7 };

            _service = new InvoicePublicationService(
                _store,
                _hoaDonStore,
                _unitOfWork,
                _accessService,
                _emailService,
                _documentExporter,
                _vietQRService,
                new VietQrSettings(),
                _notifier,
                _options,
                _logger);

            // Nhân viên 10 được phân công chi nhánh 1. Admin 1 toàn quyền (nhưng ta mô phỏng thủ công qua HashSet).
            _accessService.Permissions.Add((10, 1, EmployeeActionCodes.InvoiceSend));
            _accessService.Permissions.Add((10, 1, EmployeeActionCodes.InvoiceResendEmail));
        }

        private static HoaDon CreateDaChotInvoice(int id, int hopDongId = 100, decimal tongTien = 1_000_000m, string ma = "HD-1")
        {
            return new HoaDon
            {
                HoaDonId = id,
                MaHoaDon = ma,
                HopDongId = hopDongId,
                TongTien = tongTien,
                Thang = 9,
                Nam = 2026,
                TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaChot,
                TrangThaiHoaDon = TrangThaiHoaDon.ChuaThanhToan
            };
        }

        private static InvoicePublicationSnapshot WithStatus(InvoicePublicationSnapshot s, TrangThaiPhatHanhHoaDon status, bool? isDeleted = null)
        {
            return new InvoicePublicationSnapshot
            {
                HoaDonId = s.HoaDonId,
                MaHoaDon = s.MaHoaDon,
                HopDongId = s.HopDongId,
                ChiNhanhId = s.ChiNhanhId,
                TrangThaiPhatHanh = status,
                IsDeleted = isDeleted ?? s.IsDeleted
            };
        }

        private void SeedPublishable(int id, int hopDongId, int chiNhanhId = 1, string ma = "HD-1", int? nguoiDungId = 5, string? email = "tenant@test.com")
        {
            _store.Snapshots[id] = new InvoicePublicationSnapshot
            {
                HoaDonId = id,
                MaHoaDon = ma,
                HopDongId = hopDongId,
                ChiNhanhId = chiNhanhId,
                TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaChot,
                IsDeleted = false
            };
            _store.Invoices[id] = CreateDaChotInvoice(id, hopDongId, ma: ma);
            _store.Recipients[hopDongId] = new InvoiceRecipientInfo
            {
                NguoiThueId = 900 + hopDongId,
                HoVaTen = "Khách " + hopDongId,
                Email = email,
                NguoiDungId = nguoiDungId
            };
            _hoaDonStore.Details[id] = new HoaDonChiTietRes { HoaDonId = id, MaHoaDon = ma };
        }

        // ---------- PublishAsync: input validation ----------

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public async Task Publish_InvalidActorId_FailsWithoutStoreOrTransaction(int actorId)
        {
            var result = await _service.PublishAsync(new PublishInvoicesRequest { HoaDonIds = new List<int> { 1 } }, actorId);

            Assert.False(result.Success);
            Assert.Equal("Người thực hiện không hợp lệ.", result.Message);
            Assert.Equal(0, _store.GetSnapshotCalls);
            Assert.Equal(0, _unitOfWork.BeginTransactionCount);
        }

        [Fact]
        public async Task Publish_NullOrEmptyIds_Fails()
        {
            var resultNull = await _service.PublishAsync(new PublishInvoicesRequest { HoaDonIds = null! }, 10);
            var resultEmpty = await _service.PublishAsync(new PublishInvoicesRequest { HoaDonIds = new List<int>() }, 10);

            Assert.False(resultNull.Success);
            Assert.Equal("Vui lòng chọn ít nhất một hóa đơn để công bố.", resultNull.Message);
            Assert.False(resultEmpty.Success);
            Assert.Equal("Vui lòng chọn ít nhất một hóa đơn để công bố.", resultEmpty.Message);
            Assert.Equal(0, _store.GetSnapshotCalls);
        }

        [Fact]
        public async Task Publish_HasNonPositiveId_Fails()
        {
            var result = await _service.PublishAsync(new PublishInvoicesRequest { HoaDonIds = new List<int> { 1, 0 } }, 10);

            Assert.False(result.Success);
            Assert.Equal("Mã hóa đơn không hợp lệ.", result.Message);
            Assert.Equal(0, _store.GetSnapshotCalls);
        }

        [Fact]
        public async Task Publish_MoreThan50DistinctIds_Fails()
        {
            var ids = Enumerable.Range(1, 51).ToList();
            var result = await _service.PublishAsync(new PublishInvoicesRequest { HoaDonIds = ids }, 10);

            Assert.False(result.Success);
            Assert.Equal("Mỗi lần chỉ được công bố tối đa 50 hóa đơn.", result.Message);
            Assert.Equal(0, _store.GetSnapshotCalls);
        }

        [Fact]
        public async Task Publish_Exactly50DistinctIds_Processed()
        {
            var ids = Enumerable.Range(1, 50).ToList();
            foreach (var id in ids)
            {
                SeedPublishable(id, hopDongId: 1000 + id, nguoiDungId: null, email: null);
            }

            var result = await _service.PublishAsync(new PublishInvoicesRequest { HoaDonIds = ids }, 10);

            Assert.True(result.Success);
            Assert.Equal(50, result.Data!.Items.Count);
            Assert.Equal(50, result.Data.PublishedCount);
        }

        [Fact]
        public async Task Publish_DuplicateIds_ProcessedOnce_OrderedAscending()
        {
            SeedPublishable(5, hopDongId: 501, nguoiDungId: null, email: null);
            SeedPublishable(2, hopDongId: 502, nguoiDungId: null, email: null);

            var result = await _service.PublishAsync(new PublishInvoicesRequest { HoaDonIds = new List<int> { 5, 2, 5, 2 } }, 10);

            Assert.True(result.Success);
            Assert.Equal(2, result.Data!.Items.Count);
            Assert.Equal(2, result.Data.Items[0].HoaDonId);
            Assert.Equal(5, result.Data.Items[1].HoaDonId);
        }

        // ---------- PublishAsync: happy path ----------

        [Fact]
        public async Task Publish_DaChot_WithAccountAndEmail_PublishesNotifiesAndEmails()
        {
            SeedPublishable(1, hopDongId: 100, nguoiDungId: 55, email: "tenant@test.com");

            var result = await _service.PublishAsync(new PublishInvoicesRequest { HoaDonIds = new List<int> { 1 } }, 10);

            Assert.True(result.Success);
            var item = result.Data!.Items.Single();
            Assert.Equal(InvoicePublishOutcome.Published, item.Publish);
            Assert.Equal("Đã công bố", item.PublishMessage);
            Assert.Equal(InvoicePortalNotificationOutcome.Created, item.PortalNotification);
            Assert.Equal(InvoiceEmailOutcome.Sent, item.Email);
            Assert.Equal(1, result.Data.PublishedCount);

            var invoice = _store.Invoices[1];
            Assert.Equal(TrangThaiPhatHanhHoaDon.DaGui, invoice.TrangThaiPhatHanh);
            Assert.NotNull(invoice.NgayGui);
            Assert.NotNull(invoice.HanThanhToan);
            Assert.Equal(InvoiceDueDateCalculator.Compute(invoice.NgayGui!.Value, 7), invoice.HanThanhToan);

            var thongBao = Assert.Single(_store.AddedNotifications);
            Assert.Equal(55, thongBao.NguoiDungId);
            Assert.Equal($"Hóa đơn tháng {invoice.Thang}/{invoice.Nam}", thongBao.TieuDe);
            Assert.Equal("HoaDon", thongBao.LinhVuc);
            Assert.Equal($"/KhachThue/HoaDon?hoaDonId={invoice.HoaDonId}", thongBao.LinkDieuHuong);
            Assert.Contains(invoice.MaHoaDon, thongBao.NoiDung);
            Assert.Contains(string.Format("{0:#,##0}", invoice.TongTien), thongBao.NoiDung);
            Assert.Contains($"hết ngày {invoice.HanThanhToan!.Value.AddHours(7).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)}", thongBao.NoiDung);

            Assert.Equal(1, _emailService.SendCount);

            // commit phải xảy ra trước khi gọi notifier và email
            var commitIndex = _unitOfWork.EventLog.IndexOf("commit");
            var notifierIndex = _unitOfWork.EventLog.IndexOf("notifier");
            var emailIndex = _unitOfWork.EventLog.IndexOf("email");
            Assert.True(commitIndex < notifierIndex);
            Assert.True(commitIndex < emailIndex);
        }

        [Fact]
        public async Task Publish_ExistingHanThanhToan_IsKept_AndUsedInNotification()
        {
            SeedPublishable(1, hopDongId: 100, nguoiDungId: 55, email: "tenant@test.com");
            var existingHan = DateTime.UtcNow.AddDays(30);
            _store.Invoices[1].HanThanhToan = existingHan;

            var result = await _service.PublishAsync(new PublishInvoicesRequest { HoaDonIds = new List<int> { 1 } }, 10);

            Assert.True(result.Success);
            Assert.Equal(existingHan, _store.Invoices[1].HanThanhToan);
            Assert.Equal(existingHan, result.Data!.Items.Single().HanThanhToanUtc);
            Assert.Contains(existingHan.AddHours(7).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture), Assert.Single(_store.AddedNotifications).NoiDung);
        }

        [Fact]
        public async Task Publish_NoPortalAccount_StillPublishesAndSendsEmail()
        {
            SeedPublishable(1, hopDongId: 100, nguoiDungId: null, email: "tenant@test.com");

            var result = await _service.PublishAsync(new PublishInvoicesRequest { HoaDonIds = new List<int> { 1 } }, 10);

            Assert.True(result.Success);
            var item = result.Data!.Items.Single();
            Assert.Equal(InvoicePublishOutcome.Published, item.Publish);
            Assert.Equal(InvoicePortalNotificationOutcome.NoPortalAccount, item.PortalNotification);
            Assert.Empty(_store.AddedNotifications);
            Assert.Equal(InvoiceEmailOutcome.Sent, item.Email);
            Assert.Equal(1, _emailService.SendCount);
        }

        [Fact]
        public async Task Publish_NoEmail_ReturnsNoEmail_WithoutCallingEmailService()
        {
            SeedPublishable(1, hopDongId: 100, nguoiDungId: 55, email: null);

            var result = await _service.PublishAsync(new PublishInvoicesRequest { HoaDonIds = new List<int> { 1 } }, 10);

            Assert.True(result.Success);
            var item = result.Data!.Items.Single();
            Assert.Equal(InvoicePublishOutcome.Published, item.Publish);
            Assert.Equal(InvoiceEmailOutcome.NoEmail, item.Email);
            Assert.Equal("Khách thuê chưa có địa chỉ email.", item.EmailMessage);
            Assert.Equal(0, _emailService.SendCount);
            // Vẫn được công bố dù thiếu email
            Assert.Equal(TrangThaiPhatHanhHoaDon.DaGui, _store.Invoices[1].TrangThaiPhatHanh);
        }

        [Fact]
        public async Task Publish_SmtpReturnsFailure_EmailFailedWithSafeMessage_InvoiceStillDaGui()
        {
            SeedPublishable(1, hopDongId: 100, nguoiDungId: 55, email: "tenant@test.com");
            _emailService.Result = (false, "smtp detail nhạy cảm");

            var result = await _service.PublishAsync(new PublishInvoicesRequest { HoaDonIds = new List<int> { 1 } }, 10);

            Assert.True(result.Success);
            var item = result.Data!.Items.Single();
            Assert.Equal(InvoicePublishOutcome.Published, item.Publish);
            Assert.Equal(InvoiceEmailOutcome.Failed, item.Email);
            Assert.DoesNotContain("smtp detail nhạy cảm", item.EmailMessage);
            Assert.Equal(TrangThaiPhatHanhHoaDon.DaGui, _store.Invoices[1].TrangThaiPhatHanh);
        }

        [Fact]
        public async Task Publish_SmtpThrows_EmailFailed_NoRollbackOfPublish()
        {
            SeedPublishable(1, hopDongId: 100, nguoiDungId: 55, email: "tenant@test.com");
            _emailService.ExceptionToThrow = new InvalidOperationException("smtp exception detail nhạy cảm");

            var result = await _service.PublishAsync(new PublishInvoicesRequest { HoaDonIds = new List<int> { 1 } }, 10);

            Assert.True(result.Success);
            var item = result.Data!.Items.Single();
            Assert.Equal(InvoicePublishOutcome.Published, item.Publish);
            Assert.Equal(InvoiceEmailOutcome.Failed, item.Email);
            Assert.DoesNotContain("nhạy cảm", item.EmailMessage);
            Assert.Equal(TrangThaiPhatHanhHoaDon.DaGui, _store.Invoices[1].TrangThaiPhatHanh);
            Assert.DoesNotContain(_logger.Entries, e => e.Level >= LogLevel.Error);
        }

        [Fact]
        public async Task Publish_ExporterThrows_EmailFailed_NoRollbackOfPublish()
        {
            SeedPublishable(1, hopDongId: 100, nguoiDungId: 55, email: "tenant@test.com");
            _documentExporter.PdfException = new InvalidOperationException("pdf exception detail nhạy cảm");

            var result = await _service.PublishAsync(new PublishInvoicesRequest { HoaDonIds = new List<int> { 1 } }, 10);

            Assert.True(result.Success);
            var item = result.Data!.Items.Single();
            Assert.Equal(InvoicePublishOutcome.Published, item.Publish);
            Assert.Equal(InvoiceEmailOutcome.Failed, item.Email);
            Assert.Equal(0, _emailService.SendCount);
            Assert.Equal(TrangThaiPhatHanhHoaDon.DaGui, _store.Invoices[1].TrangThaiPhatHanh);
        }

        [Fact]
        public async Task Publish_HoaDonDetailNull_EmailFailed_NoRollbackOfPublish()
        {
            SeedPublishable(1, hopDongId: 100, nguoiDungId: 55, email: "tenant@test.com");
            _hoaDonStore.Details.Remove(1);

            var result = await _service.PublishAsync(new PublishInvoicesRequest { HoaDonIds = new List<int> { 1 } }, 10);

            Assert.True(result.Success);
            var item = result.Data!.Items.Single();
            Assert.Equal(InvoicePublishOutcome.Published, item.Publish);
            Assert.Equal(InvoiceEmailOutcome.Failed, item.Email);
            Assert.Equal(0, _emailService.SendCount);
        }

        [Fact]
        public async Task Publish_NotifierThrows_ResultUnchanged_NoErrorLog()
        {
            SeedPublishable(1, hopDongId: 100, nguoiDungId: 55, email: "tenant@test.com");
            _notifier.ExceptionToThrow = new InvalidOperationException("signalr down");

            var result = await _service.PublishAsync(new PublishInvoicesRequest { HoaDonIds = new List<int> { 1 } }, 10);

            Assert.True(result.Success);
            var item = result.Data!.Items.Single();
            Assert.Equal(InvoicePublishOutcome.Published, item.Publish);
            Assert.Equal(InvoiceEmailOutcome.Sent, item.Email);
            Assert.DoesNotContain(_logger.Entries, e => e.Level >= LogLevel.Error);
        }

        [Fact]
        public async Task Publish_AlreadyDaGui_ReturnsAlreadyPublished_RollsBack_NoSideEffects()
        {
            SeedPublishable(1, hopDongId: 100, nguoiDungId: 55, email: "tenant@test.com");
            _store.Invoices[1].TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaGui;
            var ngayGuiCu = DateTime.UtcNow.AddDays(-3);
            var hanCu = DateTime.UtcNow.AddDays(4);
            _store.Invoices[1].NgayGui = ngayGuiCu;
            _store.Invoices[1].HanThanhToan = hanCu;

            var result = await _service.PublishAsync(new PublishInvoicesRequest { HoaDonIds = new List<int> { 1 } }, 10);

            Assert.True(result.Success);
            var item = result.Data!.Items.Single();
            Assert.Equal(InvoicePublishOutcome.AlreadyPublished, item.Publish);
            Assert.Equal("Đã công bố trước đó", item.PublishMessage);
            Assert.Equal(InvoiceEmailOutcome.NotAttempted, item.Email);
            Assert.Empty(_store.AddedNotifications);
            Assert.Equal(ngayGuiCu, _store.Invoices[1].NgayGui);
            Assert.Equal(hanCu, _store.Invoices[1].HanThanhToan);
            Assert.Equal(0, _emailService.SendCount);
            Assert.True(_unitOfWork.Transactions.Single().RolledBack);
        }

        [Theory]
        [InlineData(TrangThaiPhatHanhHoaDon.DaHuy)]
        public async Task Publish_CancelledStatus_ReturnsCancelled(TrangThaiPhatHanhHoaDon status)
        {
            SeedPublishable(1, hopDongId: 100, nguoiDungId: 55, email: "tenant@test.com");
            _store.Snapshots[1] = WithStatus(_store.Snapshots[1], status);

            var result = await _service.PublishAsync(new PublishInvoicesRequest { HoaDonIds = new List<int> { 1 } }, 10);

            Assert.True(result.Success);
            var item = result.Data!.Items.Single();
            Assert.Equal(InvoicePublishOutcome.Cancelled, item.Publish);
        }

        [Fact]
        public async Task Publish_DeletedSnapshot_ReturnsCancelled_WithoutOpeningTransaction()
        {
            SeedPublishable(1, hopDongId: 100, nguoiDungId: 55, email: "tenant@test.com");
            _store.Snapshots[1] = WithStatus(_store.Snapshots[1], _store.Snapshots[1].TrangThaiPhatHanh, isDeleted: true);

            var result = await _service.PublishAsync(new PublishInvoicesRequest { HoaDonIds = new List<int> { 1 } }, 10);

            var item = result.Data!.Items.Single();
            Assert.Equal(InvoicePublishOutcome.Cancelled, item.Publish);
            Assert.Equal(0, _unitOfWork.BeginTransactionCount);
        }

        // ---------- L4: khóa hàng ra trạng thái khác snapshot ----------

        [Theory]
        [InlineData(TrangThaiPhatHanhHoaDon.DaHuy, false)]
        [InlineData(TrangThaiPhatHanhHoaDon.DaChot, true)]
        public async Task Publish_SnapshotDaChot_LockedRowCancelledOrDeleted_ReturnsCancelled_RollsBack(TrangThaiPhatHanhHoaDon lockedStatus, bool lockedIsDeleted)
        {
            SeedPublishable(1, hopDongId: 100, nguoiDungId: 55, email: "tenant@test.com");
            // Snapshot vẫn DaChot, chưa xóa; chỉ dòng đã khóa (LockInvoiceAsync) mới đổi trạng thái/IsDeleted.
            _store.Invoices[1].TrangThaiPhatHanh = lockedStatus;
            _store.Invoices[1].IsDeleted = lockedIsDeleted;

            var result = await _service.PublishAsync(new PublishInvoicesRequest { HoaDonIds = new List<int> { 1 } }, 10);

            Assert.True(result.Success);
            var item = result.Data!.Items.Single();
            Assert.Equal(InvoicePublishOutcome.Cancelled, item.Publish);
            Assert.Equal("Hóa đơn đã bị hủy, không thể công bố.", item.PublishMessage);

            var tx = Assert.Single(_unitOfWork.Transactions);
            Assert.True(tx.RolledBack);
            Assert.False(tx.Committed);
            Assert.Equal(0, _unitOfWork.SaveChangesCount);
            Assert.Empty(_store.AddedNotifications);
            Assert.Equal(0, _emailService.SendCount);
        }

        [Fact]
        public async Task Publish_LockReturnsNull_ReturnsNotFound_RollsBack()
        {
            SeedPublishable(1, hopDongId: 100, nguoiDungId: 55, email: "tenant@test.com");
            _store.Invoices.Remove(1);

            var result = await _service.PublishAsync(new PublishInvoicesRequest { HoaDonIds = new List<int> { 1 } }, 10);

            Assert.True(result.Success);
            var item = result.Data!.Items.Single();
            Assert.Equal(InvoicePublishOutcome.NotFound, item.Publish);
            Assert.Equal("Không tìm thấy hóa đơn.", item.PublishMessage);
            Assert.Equal(1, _unitOfWork.BeginTransactionCount);

            var tx = Assert.Single(_unitOfWork.Transactions);
            Assert.True(tx.RolledBack);
            Assert.Equal(0, _unitOfWork.SaveChangesCount);
            Assert.Equal(0, _emailService.SendCount);
        }

        [Theory]
        [InlineData(TrangThaiPhatHanhHoaDon.Nhap)]
        [InlineData(TrangThaiPhatHanhHoaDon.ChoDuyet)]
        public async Task Publish_NotYetFinalized_ReturnsInvalidState(TrangThaiPhatHanhHoaDon status)
        {
            SeedPublishable(1, hopDongId: 100, nguoiDungId: 55, email: "tenant@test.com");
            _store.Snapshots[1] = WithStatus(_store.Snapshots[1], status);
            _store.Invoices[1].TrangThaiPhatHanh = status;

            var result = await _service.PublishAsync(new PublishInvoicesRequest { HoaDonIds = new List<int> { 1 } }, 10);

            var item = result.Data!.Items.Single();
            Assert.Equal(InvoicePublishOutcome.InvalidState, item.Publish);
            Assert.Equal("Chỉ hóa đơn đã chốt mới được công bố.", item.PublishMessage);
        }

        [Fact]
        public async Task Publish_NotFoundSnapshot_ReturnsNotFound_WithoutOpeningTransaction()
        {
            var result = await _service.PublishAsync(new PublishInvoicesRequest { HoaDonIds = new List<int> { 999 } }, 10);

            var item = result.Data!.Items.Single();
            Assert.Equal(InvoicePublishOutcome.NotFound, item.Publish);
            Assert.Equal(0, _unitOfWork.BeginTransactionCount);
        }

        [Fact]
        public async Task Publish_ForbiddenBranch_ReturnsForbidden_WithoutOpeningTransaction()
        {
            SeedPublishable(1, hopDongId: 100, chiNhanhId: 2, nguoiDungId: 55, email: "tenant@test.com");

            var result = await _service.PublishAsync(new PublishInvoicesRequest { HoaDonIds = new List<int> { 1 } }, 10);

            var item = result.Data!.Items.Single();
            Assert.Equal(InvoicePublishOutcome.Forbidden, item.Publish);
            Assert.Equal("Bạn không có quyền công bố hóa đơn tại chi nhánh này.", item.PublishMessage);
            Assert.Equal(0, _unitOfWork.BeginTransactionCount);
            Assert.Contains((10, 2, EmployeeActionCodes.InvoiceSend), _accessService.Calls);
        }

        [Fact]
        public async Task Publish_Batch_SecondSaveThrows_OthersPublished()
        {
            SeedPublishable(1, hopDongId: 100, nguoiDungId: null, email: null, ma: "HD-1");
            SeedPublishable(2, hopDongId: 200, nguoiDungId: null, email: null, ma: "HD-2");
            SeedPublishable(3, hopDongId: 300, nguoiDungId: null, email: null, ma: "HD-3");
            _unitOfWork.ThrowOnSaveNumber = 2;

            var result = await _service.PublishAsync(new PublishInvoicesRequest { HoaDonIds = new List<int> { 1, 2, 3 } }, 10);

            Assert.True(result.Success);
            var items = result.Data!.Items.OrderBy(i => i.HoaDonId).ToList();
            Assert.Equal(InvoicePublishOutcome.Published, items[0].Publish);
            Assert.Equal(InvoicePublishOutcome.Error, items[1].Publish);
            Assert.Equal("Không thể công bố hóa đơn. Vui lòng thử lại.", items[1].PublishMessage);
            Assert.Equal(InvoicePublishOutcome.Published, items[2].Publish);
            Assert.Equal(2, result.Data.PublishedCount);
        }

        [Fact]
        public async Task Publish_Permission_AdminOrAssignedStaffAllowed_UnassignedForbidden_UsesInvoiceSendAndSnapshotBranch()
        {
            SeedPublishable(1, hopDongId: 100, chiNhanhId: 1, nguoiDungId: null, email: null);
            SeedPublishable(2, hopDongId: 200, chiNhanhId: 1, nguoiDungId: null, email: null);

            var resultAssigned = await _service.PublishAsync(new PublishInvoicesRequest { HoaDonIds = new List<int> { 1 } }, 10);
            Assert.Equal(InvoicePublishOutcome.Published, resultAssigned.Data!.Items.Single().Publish);

            var resultUnassigned = await _service.PublishAsync(new PublishInvoicesRequest { HoaDonIds = new List<int> { 2 } }, 20);
            Assert.Equal(InvoicePublishOutcome.Forbidden, resultUnassigned.Data!.Items.Single().Publish);

            Assert.Contains((10, 1, EmployeeActionCodes.InvoiceSend), _accessService.Calls);
            Assert.Contains((20, 1, EmployeeActionCodes.InvoiceSend), _accessService.Calls);
        }

        // ---------- M1: mỗi hóa đơn một transaction, dispose/rollback đúng số lần ----------

        [Fact]
        public async Task Publish_RollbackThrowsOnFirstInvoice_SecondStillProcessed_NoThrow()
        {
            SeedPublishable(1, hopDongId: 100, ma: "HD-1");
            SeedPublishable(2, hopDongId: 200, ma: "HD-2");
            _store.Invoices[1].TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaGui; // snapshot vẫn DaChot
            _unitOfWork.RollbackThrowsForTransactionNumbers.Add(1);

            var result = await _service.PublishAsync(new PublishInvoicesRequest { HoaDonIds = new List<int> { 1, 2 } }, 10);

            Assert.True(result.Success);
            var items = result.Data!.Items.OrderBy(i => i.HoaDonId).ToList();
            Assert.Equal(InvoicePublishOutcome.AlreadyPublished, items[0].Publish);
            Assert.Equal("Đã công bố trước đó", items[0].PublishMessage);
            Assert.Equal(InvoicePublishOutcome.Published, items[1].Publish);
            Assert.Equal(1, result.Data.PublishedCount);

            Assert.Contains(_logger.Entries, e => e.Level == LogLevel.Warning);
            Assert.DoesNotContain(_logger.Entries, e => e.Level >= LogLevel.Error);

            var tx1 = _unitOfWork.Transactions[0];
            Assert.Equal(1, tx1.RollbackCount);
            Assert.Equal(1, tx1.DisposeCount);
        }

        [Fact]
        public async Task Publish_SaveThrowsAndRollbackThrows_ItemError_NextPublished()
        {
            SeedPublishable(1, hopDongId: 100, ma: "HD-1");
            SeedPublishable(2, hopDongId: 200, ma: "HD-2");
            _unitOfWork.ThrowOnSaveNumber = 1;
            _unitOfWork.RollbackThrowsForTransactionNumbers.Add(1);

            var result = await _service.PublishAsync(new PublishInvoicesRequest { HoaDonIds = new List<int> { 1, 2 } }, 10);

            Assert.True(result.Success);
            var items = result.Data!.Items.OrderBy(i => i.HoaDonId).ToList();
            Assert.Equal(InvoicePublishOutcome.Error, items[0].Publish);
            Assert.Equal("Không thể công bố hóa đơn. Vui lòng thử lại.", items[0].PublishMessage);
            Assert.Equal(InvoicePublishOutcome.Published, items[1].Publish);

            Assert.Single(_logger.Entries, e => e.Level >= LogLevel.Error);
            Assert.Contains(_logger.Entries, e => e.Level == LogLevel.Warning);

            var tx1 = _unitOfWork.Transactions[0];
            Assert.Equal(1, tx1.RollbackCount);
            Assert.False(tx1.Committed);
            Assert.Equal(1, tx1.DisposeCount);

            Assert.DoesNotContain("DB failure", items[0].EmailMessage ?? string.Empty);
            Assert.DoesNotContain("DB failure", items[0].PublishMessage ?? string.Empty);
        }

        [Fact]
        public async Task Publish_BeginTransactionThrows_ItemError_NextPublished()
        {
            SeedPublishable(1, hopDongId: 100, ma: "HD-1");
            SeedPublishable(2, hopDongId: 200, ma: "HD-2");
            _unitOfWork.ThrowOnBeginNumber = 1;

            var result = await _service.PublishAsync(new PublishInvoicesRequest { HoaDonIds = new List<int> { 1, 2 } }, 10);

            Assert.True(result.Success);
            var items = result.Data!.Items.OrderBy(i => i.HoaDonId).ToList();
            Assert.Equal(InvoicePublishOutcome.Error, items[0].Publish);
            Assert.Equal("Không thể công bố hóa đơn. Vui lòng thử lại.", items[0].PublishMessage);
            Assert.Equal(InvoicePublishOutcome.Published, items[1].Publish);

            Assert.Single(_logger.Entries, e => e.Level >= LogLevel.Error);

            Assert.Single(_unitOfWork.Transactions);
            var tx = _unitOfWork.Transactions[0];
            Assert.True(tx.Committed);
            Assert.Equal(1, tx.DisposeCount);
        }

        [Fact]
        public async Task Publish_EveryBegunTransactionIsDisposed()
        {
            SeedPublishable(1, hopDongId: 100, ma: "HD-1");
            SeedPublishable(2, hopDongId: 200, ma: "HD-2");
            SeedPublishable(3, hopDongId: 300, ma: "HD-3");
            SeedPublishable(4, hopDongId: 400, ma: "HD-4");
            _store.Invoices[2].TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaGui; // -> AlreadyPublished
            _store.Invoices[3].TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaHuy; // -> Cancelled sau khóa
            _unitOfWork.ThrowOnSaveNumber = 2; // chỉ #1 và #4 đi tới save; lần save thứ 2 (của #4) ném lỗi

            var result = await _service.PublishAsync(new PublishInvoicesRequest { HoaDonIds = new List<int> { 1, 2, 3, 4 } }, 10);

            Assert.True(result.Success);
            var items = result.Data!.Items.OrderBy(i => i.HoaDonId).ToList();
            Assert.Equal(InvoicePublishOutcome.Published, items[0].Publish);
            Assert.Equal(InvoicePublishOutcome.AlreadyPublished, items[1].Publish);
            Assert.Equal(InvoicePublishOutcome.Cancelled, items[2].Publish);
            Assert.Equal(InvoicePublishOutcome.Error, items[3].Publish);

            Assert.Equal(4, _unitOfWork.Transactions.Count);
            Assert.Equal(4, _unitOfWork.BeginTransactionCount);
            Assert.All(_unitOfWork.Transactions, tx => Assert.Equal(1, tx.DisposeCount));

            Assert.True(_unitOfWork.Transactions[0].Committed);
            Assert.Equal(0, _unitOfWork.Transactions[0].RollbackCount);

            Assert.Equal(1, _unitOfWork.Transactions[1].RollbackCount);
            Assert.Equal(1, _unitOfWork.Transactions[2].RollbackCount);
            Assert.Equal(1, _unitOfWork.Transactions[3].RollbackCount);
        }

        [Fact]
        public async Task Publish_CommitBeforeNotifierAndEmail()
        {
            SeedPublishable(1, hopDongId: 100, nguoiDungId: 55, email: "tenant@test.com");

            var result = await _service.PublishAsync(new PublishInvoicesRequest { HoaDonIds = new List<int> { 1 } }, 10);

            Assert.True(result.Success);
            var relevantEvents = _unitOfWork.EventLog
                .Where(e => e == "commit" || e == "notifier" || e == "email" || e == "rollback")
                .ToList();
            Assert.Equal(new List<string> { "commit", "notifier", "email" }, relevantEvents);
        }

        // ---------- ResendEmailAsync ----------

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public async Task Resend_InvalidActorId_Fails_WithoutCallingEmail(int actorId)
        {
            var result = await _service.ResendEmailAsync(1, actorId);

            Assert.False(result.Success);
            Assert.Equal("Người thực hiện không hợp lệ.", result.Message);
            Assert.Equal(0, _emailService.SendCount);
        }

        [Fact]
        public async Task Resend_NotFound_Fails()
        {
            var result = await _service.ResendEmailAsync(999, 10);

            Assert.False(result.Success);
            Assert.Equal("Không tìm thấy hóa đơn.", result.Message);
            Assert.Equal(0, _emailService.SendCount);
        }

        [Fact]
        public async Task Resend_WrongBranch_Fails_ContainsKhongCoQuyen()
        {
            SeedPublishable(1, hopDongId: 100, chiNhanhId: 2, nguoiDungId: 55, email: "tenant@test.com");
            _store.Invoices[1].TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaGui;
            _store.Snapshots[1] = WithStatus(_store.Snapshots[1], TrangThaiPhatHanhHoaDon.DaGui);

            var result = await _service.ResendEmailAsync(1, 10);

            Assert.False(result.Success);
            Assert.Contains("không có quyền", result.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(0, _emailService.SendCount);
        }

        [Fact]
        public async Task Resend_DaHuyOrDeleted_Fails()
        {
            SeedPublishable(1, hopDongId: 100, nguoiDungId: 55, email: "tenant@test.com");
            _store.Snapshots[1] = WithStatus(_store.Snapshots[1], TrangThaiPhatHanhHoaDon.DaHuy);

            var result = await _service.ResendEmailAsync(1, 10);

            Assert.False(result.Success);
            Assert.Equal("Hóa đơn đã bị hủy, không thể gửi lại email.", result.Message);
            Assert.Equal(0, _emailService.SendCount);

            // Ca biên L3: DaGui nhưng đã bị xóa mềm vẫn phải bị chặn như DaHuy.
            SeedPublishable(2, hopDongId: 200, nguoiDungId: 55, email: "tenant@test.com");
            _store.Snapshots[2] = WithStatus(_store.Snapshots[2], TrangThaiPhatHanhHoaDon.DaGui, isDeleted: true);

            var deletedResult = await _service.ResendEmailAsync(2, 10);

            Assert.False(deletedResult.Success);
            Assert.Equal("Hóa đơn đã bị hủy, không thể gửi lại email.", deletedResult.Message);
            Assert.Equal(0, _emailService.SendCount);
        }

        [Theory]
        [InlineData(TrangThaiPhatHanhHoaDon.Nhap)]
        [InlineData(TrangThaiPhatHanhHoaDon.ChoDuyet)]
        [InlineData(TrangThaiPhatHanhHoaDon.DaChot)]
        public async Task Resend_NotYetDaGui_Fails(TrangThaiPhatHanhHoaDon status)
        {
            SeedPublishable(1, hopDongId: 100, nguoiDungId: 55, email: "tenant@test.com");
            _store.Snapshots[1] = WithStatus(_store.Snapshots[1], status);

            var result = await _service.ResendEmailAsync(1, 10);

            Assert.False(result.Success);
            Assert.Equal("Chỉ hóa đơn đã công bố mới được gửi lại email.", result.Message);
            Assert.Equal(0, _emailService.SendCount);
        }

        [Fact]
        public async Task Resend_DaGui_Succeeds_SendsEmail_NoTransactionNoSaveNoNotification()
        {
            SeedPublishable(1, hopDongId: 100, nguoiDungId: 55, email: "tenant@test.com");
            _store.Snapshots[1] = WithStatus(_store.Snapshots[1], TrangThaiPhatHanhHoaDon.DaGui);
            var ngayGuiCu = DateTime.UtcNow.AddDays(-2);
            _store.Invoices[1].TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaGui;
            _store.Invoices[1].NgayGui = ngayGuiCu;

            var result = await _service.ResendEmailAsync(1, 10);

            Assert.True(result.Success);
            Assert.Equal(InvoiceEmailOutcome.Sent, result.Data!.Email);
            Assert.Equal("tenant@test.com", result.Data.RecipientEmail);
            Assert.Equal(1, _emailService.SendCount);
            Assert.Equal(0, _unitOfWork.BeginTransactionCount);
            Assert.Equal(0, _unitOfWork.SaveChangesCount);
            Assert.Empty(_store.AddedNotifications);
            Assert.Equal(ngayGuiCu, _store.Invoices[1].NgayGui);
        }

        [Fact]
        public async Task Resend_DaGuiAndDaThanhToan_StillSends()
        {
            SeedPublishable(1, hopDongId: 100, nguoiDungId: 55, email: "tenant@test.com");
            _store.Snapshots[1] = WithStatus(_store.Snapshots[1], TrangThaiPhatHanhHoaDon.DaGui);
            _store.Invoices[1].TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaGui;
            _store.Invoices[1].TrangThaiHoaDon = TrangThaiHoaDon.DaThanhToan;

            var result = await _service.ResendEmailAsync(1, 10);

            Assert.True(result.Success);
            Assert.Equal(InvoiceEmailOutcome.Sent, result.Data!.Email);
        }

        [Fact]
        public async Task Resend_NoEmail_ReturnsOk_NoEmailOutcome()
        {
            SeedPublishable(1, hopDongId: 100, nguoiDungId: 55, email: null);
            _store.Snapshots[1] = WithStatus(_store.Snapshots[1], TrangThaiPhatHanhHoaDon.DaGui);

            var result = await _service.ResendEmailAsync(1, 10);

            Assert.True(result.Success);
            Assert.Equal(InvoiceEmailOutcome.NoEmail, result.Data!.Email);
            Assert.Equal(0, _emailService.SendCount);
        }

        [Fact]
        public async Task Resend_SmtpFailure_ReturnsOk_Failed_SafeMessage_LogHasNoEmail()
        {
            SeedPublishable(1, hopDongId: 100, nguoiDungId: 55, email: "tenant@test.com");
            _store.Snapshots[1] = WithStatus(_store.Snapshots[1], TrangThaiPhatHanhHoaDon.DaGui);
            _emailService.Result = (false, "smtp detail nhạy cảm");

            var result = await _service.ResendEmailAsync(1, 10);

            Assert.True(result.Success);
            Assert.Equal(InvoiceEmailOutcome.Failed, result.Data!.Email);
            Assert.DoesNotContain("smtp detail nhạy cảm", result.Data.EmailMessage);
            Assert.DoesNotContain(_logger.Messages, m => m.Contains("tenant@test.com"));
        }

        [Fact]
        public async Task Resend_SmtpThrows_ReturnsOk_Failed_SafeMessage_LogHasNoEmail()
        {
            SeedPublishable(1, hopDongId: 100, nguoiDungId: 55, email: "tenant@test.com");
            _store.Snapshots[1] = WithStatus(_store.Snapshots[1], TrangThaiPhatHanhHoaDon.DaGui);
            _emailService.ExceptionToThrow = new InvalidOperationException("smtp exception detail nhạy cảm");

            var result = await _service.ResendEmailAsync(1, 10);

            Assert.True(result.Success);
            Assert.Equal(InvoiceEmailOutcome.Failed, result.Data!.Email);
            Assert.DoesNotContain("nhạy cảm", result.Data.EmailMessage);
            Assert.DoesNotContain(_logger.Messages, m => m.Contains("tenant@test.com"));
        }

        [Fact]
        public async Task Resend_UsesInvoiceResendEmailActionCode()
        {
            SeedPublishable(1, hopDongId: 100, nguoiDungId: 55, email: "tenant@test.com");
            _store.Snapshots[1] = WithStatus(_store.Snapshots[1], TrangThaiPhatHanhHoaDon.DaGui);

            await _service.ResendEmailAsync(1, 10);

            Assert.Contains((10, 1, EmployeeActionCodes.InvoiceResendEmail), _accessService.Calls);
            Assert.DoesNotContain((10, 1, EmployeeActionCodes.InvoiceSend), _accessService.Calls);
        }

        // ---------- Kịch bản đã xóa khỏi LegacyInvoiceAuthorizationTests, viết lại theo nghiệp vụ ResendEmailAsync mới ----------
        // Bảng ánh xạ tên cũ (HoaDonService.SendInvoiceEmailAsync) -> tên mới (InvoicePublicationService.ResendEmailAsync)
        // được ghi trong .bangiao/ket-qua-test.md.

        [Fact]
        public async Task ResendLegacy_Rejects_WhenStaffNotAssignedToInvoiceBranch()
        {
            SeedPublishable(1, hopDongId: 100, chiNhanhId: 1, nguoiDungId: 55, email: "tenant@test.com");
            _store.Snapshots[1] = WithStatus(_store.Snapshots[1], TrangThaiPhatHanhHoaDon.DaGui);

            var result = await _service.ResendEmailAsync(1, actorId: 20); // nhân viên 20 chưa được phân công (không có trong Permissions)

            Assert.False(result.Success);
            Assert.Contains("không có quyền", result.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(0, _emailService.SendCount);
        }

        [Theory]
        [InlineData(TrangThaiPhatHanhHoaDon.Nhap)]
        [InlineData(TrangThaiPhatHanhHoaDon.ChoDuyet)]
        [InlineData(TrangThaiPhatHanhHoaDon.DaChot)]
        [InlineData(TrangThaiPhatHanhHoaDon.DaHuy)]
        public async Task ResendLegacy_Rejects_WhenInvoiceIsNotDaGui(TrangThaiPhatHanhHoaDon status)
        {
            SeedPublishable(1, hopDongId: 100, nguoiDungId: 55, email: "tenant@test.com");
            _store.Snapshots[1] = WithStatus(_store.Snapshots[1], status);

            var result = await _service.ResendEmailAsync(1, actorId: 10);

            Assert.False(result.Success);
            if (status == TrangThaiPhatHanhHoaDon.DaHuy)
            {
                Assert.Contains("đã bị hủy", result.Message);
            }
            else
            {
                Assert.Equal("Chỉ hóa đơn đã công bố mới được gửi lại email.", result.Message);
            }
        }

        [Fact]
        public async Task ResendLegacy_Succeeds_WhenStaffAssigned_And_InvoiceIsDaGui()
        {
            SeedPublishable(1, hopDongId: 100, nguoiDungId: 55, email: "tenant@test.com", ma: "HD-1");
            _store.Snapshots[1] = WithStatus(_store.Snapshots[1], TrangThaiPhatHanhHoaDon.DaGui);

            var result = await _service.ResendEmailAsync(1, actorId: 10);

            Assert.True(result.Success);
            Assert.Equal("HD-1", result.Data!.MaHoaDon);
            Assert.Equal("tenant@test.com", result.Data.RecipientEmail);
            Assert.Equal(1, _emailService.SendCount);
            Assert.DoesNotContain(_logger.Messages, m => m.Contains("tenant@test.com"));
        }

        [Fact]
        public async Task ResendLegacy_MissingEmail_DoesNotCallEmailService()
        {
            SeedPublishable(1, hopDongId: 100, nguoiDungId: 55, email: null);
            _store.Snapshots[1] = WithStatus(_store.Snapshots[1], TrangThaiPhatHanhHoaDon.DaGui);

            var result = await _service.ResendEmailAsync(1, actorId: 10);

            Assert.True(result.Success);
            Assert.Equal(InvoiceEmailOutcome.NoEmail, result.Data!.Email);
            Assert.Equal(0, _emailService.SendCount);
        }

        [Fact]
        public async Task ResendLegacy_EmptyPdf_DoesNotCallEmailService()
        {
            SeedPublishable(1, hopDongId: 100, nguoiDungId: 55, email: "tenant@test.com");
            _store.Snapshots[1] = WithStatus(_store.Snapshots[1], TrangThaiPhatHanhHoaDon.DaGui);
            _documentExporter.PdfBytes = Array.Empty<byte>();

            var result = await _service.ResendEmailAsync(1, actorId: 10);

            Assert.True(result.Success);
            Assert.Equal(InvoiceEmailOutcome.Failed, result.Data!.Email);
            Assert.Equal(0, _emailService.SendCount);
        }

        [Fact]
        public async Task ResendLegacy_PdfException_ReturnsSafeFailureWithoutSending()
        {
            SeedPublishable(1, hopDongId: 100, nguoiDungId: 55, email: "tenant@test.com");
            _store.Snapshots[1] = WithStatus(_store.Snapshots[1], TrangThaiPhatHanhHoaDon.DaGui);
            _documentExporter.PdfException = new InvalidOperationException("pdf exception detail");

            var result = await _service.ResendEmailAsync(1, actorId: 10);

            Assert.True(result.Success);
            Assert.Equal(InvoiceEmailOutcome.Failed, result.Data!.Email);
            Assert.Equal(0, _emailService.SendCount);
            Assert.DoesNotContain("pdf exception detail", result.Data.EmailMessage);
            Assert.DoesNotContain(_logger.Messages, m => m.Contains("tenant@test.com"));
        }

        [Fact]
        public async Task ResendLegacy_SmtpFailure_ReturnsSafeFailure()
        {
            SeedPublishable(1, hopDongId: 100, nguoiDungId: 55, email: "tenant@test.com");
            _store.Snapshots[1] = WithStatus(_store.Snapshots[1], TrangThaiPhatHanhHoaDon.DaGui);
            _emailService.Result = (false, "smtp detail");

            var result = await _service.ResendEmailAsync(1, actorId: 10);

            Assert.True(result.Success);
            Assert.Equal(InvoiceEmailOutcome.Failed, result.Data!.Email);
            Assert.Equal(1, _emailService.SendCount);
            Assert.DoesNotContain("smtp detail", result.Data.EmailMessage);
            Assert.DoesNotContain(_logger.Messages, m => m.Contains("tenant@test.com"));
        }

        [Fact]
        public async Task ResendLegacy_SmtpException_ReturnsSafeFailure()
        {
            SeedPublishable(1, hopDongId: 100, nguoiDungId: 55, email: "tenant@test.com");
            _store.Snapshots[1] = WithStatus(_store.Snapshots[1], TrangThaiPhatHanhHoaDon.DaGui);
            _emailService.ExceptionToThrow = new InvalidOperationException("smtp exception detail");

            var result = await _service.ResendEmailAsync(1, actorId: 10);

            Assert.True(result.Success);
            Assert.Equal(InvoiceEmailOutcome.Failed, result.Data!.Email);
            Assert.Equal(1, _emailService.SendCount);
            Assert.DoesNotContain("smtp exception detail", result.Data.EmailMessage);
            Assert.DoesNotContain(_logger.Messages, m => m.Contains("tenant@test.com"));
        }
    }
}
