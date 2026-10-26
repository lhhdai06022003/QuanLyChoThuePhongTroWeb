using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using QuanLyChoThuePhongTroWeb.Application.Common.Configurations;
using QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;
using QuanLyChoThuePhongTroWeb.Application.UnitTests.Fakes;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    // T1: log của Publisher và Dispatcher ghi đúng kênh và khóa chống trùng; kênh VNPay không còn ra "SePay 0".
    public class PaymentLogLabelTests
    {
        private static readonly DateTime PayTime = new(2026, 10, 3, 2, 30, 0, DateTimeKind.Utc);
        private const string Content = "NOI-DUNG-CHUYEN-KHOAN-BI-MAT";
        private const string Description = "MO-TA-BI-MAT";

        private readonly RecordingNotifier _notifier = new();
        private readonly RecordingPaymentNotificationQueue _queue = new();
        private readonly CapturingLogger<PaymentOutcomePublisher> _log = new();
        private readonly PaymentOutcomePublisher _publisher;

        public PaymentLogLabelTests()
        {
            _publisher = new PaymentOutcomePublisher(_notifier, _queue, new FakeTransferAlertDeduplicator(), _log);
        }

        private static IncomingBankTransfer SePayTransfer() => new(
            92704, "MBBank", PayTime, "0123456789", null, null, Content, "in", 2_000_000m, "FT26ABC", Description);

        private static IncomingBankTransfer VnPayTransfer() => new(
            0, "NCB", PayTime, null, null, null, Content, "in", 2_000_000m, "14123456", Description)
        {
            Kenh = OnlinePaymentChannel.VnPay,
            CardType = "ATM"
        };

        private static TransferOutcome Outcome(TransferOutcomeKind kind, IncomingBankTransfer transfer, int? lichSuThanhToanId = 77) => new()
        {
            Kind = kind,
            Transfer = transfer,
            Code = "TTPTHD7",
            HoaDonId = 7,
            MaHoaDon = "HD-7",
            LichSuThanhToanId = kind == TransferOutcomeKind.Recorded ? lichSuThanhToanId : null,
            TenantUserId = kind == TransferOutcomeKind.Recorded ? 500 : null,
            TongTien = 2_000_000m,
            DaThu = 2_000_000m,
            ConLai = 0m,
            TrangThaiThanhToan = "Đã thanh toán"
        };

        private static void AssertNoSecretText(string message)
        {
            Assert.DoesNotContain(Content, message);
            Assert.DoesNotContain(Description, message);
        }

        [Fact]
        public void LogLabel_NamesTheChannelAndTheDedupKey()
        {
            Assert.Equal("SePay 92704 (SEPAY-FT26ABC)", SePayTransfer().LogLabel);
            Assert.Equal("VNPay VNPAY-14123456", VnPayTransfer().LogLabel);
            Assert.Equal("SePay 5 (SEPAY-ID-5)", (SePayTransfer() with { SePayId = 5, ReferenceCode = null }).LogLabel);
        }

        [Fact]
        public async Task Publisher_VnPaySignalRFailure_LogsTheVnPayKey_NotSePayZero()
        {
            _notifier.Throw = true;

            await _publisher.PublishAsync(Outcome(TransferOutcomeKind.Recorded, VnPayTransfer()));

            var entry = Assert.Single(_log.Entries);
            Assert.Equal(LogLevel.Warning, entry.Level);
            Assert.Contains("VNPay VNPAY-14123456", entry.Message);
            Assert.DoesNotContain("SePay 0", entry.Message);
            AssertNoSecretText(entry.Message);
        }

        [Fact]
        public async Task Publisher_SePaySignalRFailure_StillLogsTheSePayIdAndKey()
        {
            _notifier.Throw = true;

            await _publisher.PublishAsync(Outcome(TransferOutcomeKind.Recorded, SePayTransfer()));

            var entry = Assert.Single(_log.Entries);
            Assert.Contains("SePay 92704 (SEPAY-FT26ABC)", entry.Message);
            AssertNoSecretText(entry.Message);
        }

        [Fact]
        public async Task Publisher_VnPayWithAFullQueue_LogsTheVnPayKey()
        {
            _queue.Accept = false;

            await _publisher.PublishAsync(Outcome(TransferOutcomeKind.Recorded, VnPayTransfer()));

            var entry = Assert.Single(_log.Entries);
            Assert.Equal(LogLevel.Warning, entry.Level);
            Assert.Contains("VNPay VNPAY-14123456", entry.Message);
            Assert.Contains("hóa đơn 7", entry.Message);
            Assert.Contains("khoản 77", entry.Message);
            AssertNoSecretText(entry.Message);
        }

        [Fact]
        public async Task Publisher_VnPayRepeatedAlert_LogsTheVnPayKey()
        {
            var outcome = Outcome(TransferOutcomeKind.AlreadyPaid, VnPayTransfer());

            await _publisher.PublishAsync(outcome);
            await _publisher.PublishAsync(outcome);

            var entry = Assert.Single(_log.Entries);
            Assert.Equal(LogLevel.Information, entry.Level);
            Assert.Contains("VNPay VNPAY-14123456", entry.Message);
            AssertNoSecretText(entry.Message);
        }

        [Fact]
        public async Task Dispatcher_VnPayRecordedWithoutALedgerRow_LogsTheVnPayKey()
        {
            var h = new PaymentTestHarness();
            var log = new CapturingLogger<PaymentNotificationDispatcher>();
            var dispatcher = new PaymentNotificationDispatcher(
                h.Store, new FakeTelegramNotifier(), new RecordingPaymentReceiptEmailService(),
                new TelegramOptions(), new OnlinePaymentOptions(), log);

            await dispatcher.DispatchAsync(new PaymentNotificationWorkItem(Outcome(TransferOutcomeKind.Recorded, VnPayTransfer(), lichSuThanhToanId: null)));

            var entry = Assert.Single(log.Entries);
            Assert.Equal(LogLevel.Warning, entry.Level);
            Assert.Contains("VNPAY-14123456", entry.Message);
            Assert.DoesNotContain("SePay 0", entry.Message);
            AssertNoSecretText(entry.Message);
        }

        [Fact]
        public async Task Dispatcher_SePayRecordedWithoutALedgerRow_LogsTheSePayKey()
        {
            var h = new PaymentTestHarness();
            var log = new CapturingLogger<PaymentNotificationDispatcher>();
            var dispatcher = new PaymentNotificationDispatcher(
                h.Store, new FakeTelegramNotifier(), new RecordingPaymentReceiptEmailService(),
                new TelegramOptions(), new OnlinePaymentOptions(), log);

            await dispatcher.DispatchAsync(new PaymentNotificationWorkItem(Outcome(TransferOutcomeKind.Recorded, SePayTransfer(), lichSuThanhToanId: null)));

            var entry = Assert.Single(log.Entries);
            Assert.Contains("SePay 92704 (SEPAY-FT26ABC)", entry.Message);
            AssertNoSecretText(entry.Message);
        }
    }
}
