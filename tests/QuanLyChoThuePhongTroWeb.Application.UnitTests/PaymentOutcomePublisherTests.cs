using Microsoft.Extensions.Logging;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Notifications;
using QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;
using QuanLyChoThuePhongTroWeb.Application.UnitTests.Fakes;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    // Việc sau khi ghi sổ: sự kiện SignalR cho panel của khách và hàng đợi Telegram + email (spec thanh toán trực tuyến §5.3, §6.1).
    public class PaymentOutcomePublisherTests
    {
        private const int TenantUserId = 500;
        private static readonly DateTime TransferTime = new(2026, 10, 3, 2, 30, 0, DateTimeKind.Utc);

        private readonly RecordingNotifier _notifier = new();
        private readonly RecordingPaymentNotificationQueue _queue = new();
        private readonly FakeTransferAlertDeduplicator _deduplicator = new();
        private readonly CapturingLogger<PaymentOutcomePublisher> _log = new();
        private readonly PaymentOutcomePublisher _publisher;

        public PaymentOutcomePublisherTests()
        {
            _publisher = new PaymentOutcomePublisher(_notifier, _queue, _deduplicator, _log);
        }

        private static IncomingBankTransfer Transfer(string? reference = "FT26ABC", long id = 92704) => new(
            id, "MBBank", TransferTime, "0123456789", null, null, "TTPTHD7", "in", 2_000_000m, reference, "TTPTHD7");

        private static TransferOutcome Outcome(TransferOutcomeKind kind, IncomingBankTransfer? transfer = null, int? tenantUserId = TenantUserId) => new()
        {
            Kind = kind,
            Transfer = transfer ?? Transfer(),
            Code = "TTPTHD7",
            HoaDonId = 7,
            MaHoaDon = "HD-7",
            LichSuThanhToanId = kind == TransferOutcomeKind.Recorded ? 77 : null,
            TenantUserId = kind == TransferOutcomeKind.Recorded ? tenantUserId : null,
            TongTien = 2_000_000m,
            DaThu = 1_500_000m,
            ConLai = 500_000m,
            TrangThaiThanhToan = "Thanh toán một phần"
        };

        // ---------- Recorded ----------

        [Fact]
        public async Task Recorded_SendsThePanelEvent_ToTheTenant_AndQueuesTheWork()
        {
            var outcome = Outcome(TransferOutcomeKind.Recorded);

            await _publisher.PublishAsync(outcome);

            var update = Assert.Single(_notifier.PaymentUpdates);
            Assert.Equal(TenantUserId, update.UserId);
            Assert.Equal(new InvoicePaymentUpdatedEvent(7, 500_000m, "Thanh toán một phần"), update.Payload);
            Assert.Same(outcome, Assert.Single(_queue.Items).Outcome);
            Assert.Empty(_log.Entries);
        }

        [Fact]
        public async Task Recorded_WithoutATenantAccount_SendsNoPanelEvent_ButStillQueuesTheWork()
        {
            await _publisher.PublishAsync(Outcome(TransferOutcomeKind.Recorded, tenantUserId: null));

            Assert.Empty(_notifier.PaymentUpdates);
            Assert.Single(_queue.Items);
            Assert.Empty(_log.Entries);
        }

        [Fact]
        public async Task Recorded_WhenSignalRFails_LogsAWarning_AndStillQueuesTheWork()
        {
            _notifier.Throw = true;

            await _publisher.PublishAsync(Outcome(TransferOutcomeKind.Recorded));

            Assert.Single(_queue.Items);
            var entry = Assert.Single(_log.Entries);
            Assert.Equal(LogLevel.Warning, entry.Level);
            Assert.Contains("92704", entry.Message);
            Assert.Contains("7", entry.Message);
        }

        [Fact]
        public async Task Recorded_IsNotDeduplicated_SoEveryRecordedRowIsQueued()
        {
            await _publisher.PublishAsync(Outcome(TransferOutcomeKind.Recorded));
            await _publisher.PublishAsync(Outcome(TransferOutcomeKind.Recorded));

            Assert.Equal(2, _queue.Items.Count);
        }

        [Fact]
        public async Task Recorded_WhenTheQueueIsFull_LogsAWarning_WithoutThrowing()
        {
            _queue.Accept = false;

            await _publisher.PublishAsync(Outcome(TransferOutcomeKind.Recorded));

            Assert.Single(_notifier.PaymentUpdates);
            Assert.Empty(_queue.Items);
            var entry = Assert.Single(_log.Entries);
            Assert.Equal(LogLevel.Warning, entry.Level);
            Assert.Contains("Hàng đợi thông báo thanh toán đầy", entry.Message);
        }

        [Fact]
        public async Task Recorded_WhenTheQueueThrows_LogsAWarning_WithoutThrowing()
        {
            _queue.Throw = true;

            await _publisher.PublishAsync(Outcome(TransferOutcomeKind.Recorded));

            Assert.Single(_notifier.PaymentUpdates);
            var entry = Assert.Single(_log.Entries);
            Assert.Equal(LogLevel.Warning, entry.Level);
        }

        // ---------- Cảnh báo ----------

        public static TheoryData<TransferOutcomeKind> AlertKinds => new()
        {
            TransferOutcomeKind.WrongAccount,
            TransferOutcomeKind.Unmatched,
            TransferOutcomeKind.InvoiceNotPayable,
            TransferOutcomeKind.AlreadyPaid
        };

        [Theory]
        [MemberData(nameof(AlertKinds))]
        public async Task Alert_IsQueued_OncePerDedupKey(TransferOutcomeKind kind)
        {
            var transfer = Transfer();

            await _publisher.PublishAsync(Outcome(kind, transfer));
            await _publisher.PublishAsync(Outcome(kind, transfer));

            Assert.Single(_queue.Items);
            Assert.Empty(_notifier.PaymentUpdates);
            var skipped = Assert.Single(_log.Entries);
            Assert.Equal(LogLevel.Information, skipped.Level);
        }

        [Theory]
        [MemberData(nameof(AlertKinds))]
        public async Task Alert_WithADifferentDedupKey_IsQueuedAgain(TransferOutcomeKind kind)
        {
            await _publisher.PublishAsync(Outcome(kind, Transfer("FT-A")));
            await _publisher.PublishAsync(Outcome(kind, Transfer("FT-B", id: 92705)));

            Assert.Equal(2, _queue.Items.Count);
            Assert.Empty(_log.Entries);
        }

        [Fact]
        public async Task Alert_WithoutAReferenceCode_UsesTheSePayIdAsTheDedupKey()
        {
            await _publisher.PublishAsync(Outcome(TransferOutcomeKind.Unmatched, Transfer(reference: null, id: 1)));
            await _publisher.PublishAsync(Outcome(TransferOutcomeKind.Unmatched, Transfer(reference: null, id: 1)));
            await _publisher.PublishAsync(Outcome(TransferOutcomeKind.Unmatched, Transfer(reference: null, id: 2)));

            Assert.Equal(2, _queue.Items.Count);
        }

        [Fact]
        public async Task Alert_WhenTheDeduplicatorFails_LogsAWarning_AndQueuesNothing()
        {
            _deduplicator.Throw = true;

            await _publisher.PublishAsync(Outcome(TransferOutcomeKind.Unmatched));

            Assert.Empty(_queue.Items);
            var entry = Assert.Single(_log.Entries);
            Assert.Equal(LogLevel.Warning, entry.Level);
        }

        [Fact]
        public async Task Alert_WhenTheQueueIsFull_LogsAWarning()
        {
            _queue.Accept = false;

            await _publisher.PublishAsync(Outcome(TransferOutcomeKind.WrongAccount));

            var entry = Assert.Single(_log.Entries);
            Assert.Equal(LogLevel.Warning, entry.Level);
            Assert.Contains("Hàng đợi thông báo thanh toán đầy", entry.Message);
        }

        // ---------- Đồng bộ bù (LaDongBoBu): Admin đang xem bảng kết quả nên không cảnh báo Telegram ----------

        [Theory]
        [MemberData(nameof(AlertKinds))]
        public async Task SyncAlert_IsNotQueued_AndNeverReachesTheDeduplicator(TransferOutcomeKind kind)
        {
            var transfer = Transfer() with { LaDongBoBu = true };

            await _publisher.PublishAsync(Outcome(kind, transfer));
            await _publisher.PublishAsync(Outcome(kind, transfer));

            Assert.Empty(_queue.Items);
            Assert.Equal(0, _deduplicator.Calls);
            Assert.Empty(_notifier.PaymentUpdates);
            Assert.Empty(_log.Entries);
        }

        [Theory]
        [MemberData(nameof(AlertKinds))]
        public async Task SyncAlert_DoesNotUseUpTheDedupKey_SoALaterWebhookRetryStillAlerts(TransferOutcomeKind kind)
        {
            await _publisher.PublishAsync(Outcome(kind, Transfer() with { LaDongBoBu = true }));
            await _publisher.PublishAsync(Outcome(kind, Transfer()));

            Assert.Single(_queue.Items);
            Assert.False(_queue.Items[0].Outcome.Transfer.LaDongBoBu);
        }

        [Fact]
        public async Task SyncRecorded_StillSendsThePanelEvent_AndQueuesTheFullNotifications()
        {
            var outcome = Outcome(TransferOutcomeKind.Recorded, Transfer() with { LaDongBoBu = true });

            await _publisher.PublishAsync(outcome);

            Assert.Single(_notifier.PaymentUpdates);
            Assert.Same(outcome, Assert.Single(_queue.Items).Outcome);
            Assert.Empty(_log.Entries);
        }

        [Fact]
        public void LaDongBoBu_IsFalseByDefault_AndDoesNotChangeTheDedupKey()
        {
            Assert.False(Transfer().LaDongBoBu);
            Assert.Equal(Transfer().DedupKey, (Transfer() with { LaDongBoBu = true }).DedupKey);
        }

        // ---------- Không làm gì ----------

        [Theory]
        [InlineData(TransferOutcomeKind.Duplicate)]
        [InlineData(TransferOutcomeKind.Ignored)]
        [InlineData(TransferOutcomeKind.Failed)]
        public async Task DuplicateIgnoredAndFailed_DoNothing(TransferOutcomeKind kind)
        {
            await _publisher.PublishAsync(Outcome(kind));
            await _publisher.PublishAsync(Outcome(kind));

            Assert.Empty(_queue.Items);
            Assert.Empty(_notifier.PaymentUpdates);
            Assert.Empty(_notifier.Sent);
            Assert.Empty(_log.Entries);
        }

        [Fact]
        public async Task Recorded_NeverGoesThroughTheAlertDeduplicator()
        {
            await _publisher.PublishAsync(Outcome(TransferOutcomeKind.Recorded));
            await _publisher.PublishAsync(Outcome(TransferOutcomeKind.Recorded));

            Assert.Equal(0, _deduplicator.Calls);
        }

        [Theory]
        [InlineData(TransferOutcomeKind.Duplicate)]
        [InlineData(TransferOutcomeKind.Ignored)]
        [InlineData(TransferOutcomeKind.Failed)]
        public async Task DuplicateIgnoredAndFailed_NeverReachTheDeduplicatorOrTheQueue(TransferOutcomeKind kind)
        {
            await _publisher.PublishAsync(Outcome(kind));

            Assert.Equal(0, _deduplicator.Calls);
            Assert.Empty(_queue.Items);
        }

        [Fact]
        public async Task ADuplicateOfARecordedTransfer_NeverSendsASecondPanelEventOrTelegramTask()
        {
            var transfer = Transfer();

            await _publisher.PublishAsync(Outcome(TransferOutcomeKind.Recorded, transfer));
            await _publisher.PublishAsync(Outcome(TransferOutcomeKind.Duplicate, transfer));
            await _publisher.PublishAsync(Outcome(TransferOutcomeKind.Duplicate, transfer));

            Assert.Single(_notifier.PaymentUpdates);
            Assert.Single(_queue.Items);
        }

        [Fact]
        public async Task Recorded_AndAnAlertOfTheSameTransfer_DoNotInterfere()
        {
            var transfer = Transfer();

            await _publisher.PublishAsync(Outcome(TransferOutcomeKind.AlreadyPaid, transfer));
            await _publisher.PublishAsync(Outcome(TransferOutcomeKind.Recorded, transfer));

            Assert.Equal(2, _queue.Items.Count);
            Assert.Single(_notifier.PaymentUpdates);
        }

        [Theory]
        [InlineData(TransferOutcomeKind.Unmatched)]
        [InlineData(TransferOutcomeKind.WrongAccount)]
        public async Task Alert_HasNoTenantPanelEvent_EvenWhenAnInvoiceIsKnown(TransferOutcomeKind kind)
        {
            var outcome = Outcome(kind) with { TenantUserId = TenantUserId };

            await _publisher.PublishAsync(outcome);

            Assert.Empty(_notifier.PaymentUpdates);
            Assert.Empty(_notifier.Sent);
            Assert.Single(_queue.Items);
        }

        [Fact]
        public async Task Recorded_PanelEventCarriesTheLatestAmountStillOwed()
        {
            var outcome = Outcome(TransferOutcomeKind.Recorded) with { ConLai = 0m, TrangThaiThanhToan = "Đã thanh toán" };

            await _publisher.PublishAsync(outcome);

            Assert.Equal(new InvoicePaymentUpdatedEvent(7, 0m, "Đã thanh toán"), Assert.Single(_notifier.PaymentUpdates).Payload);
        }

        [Fact]
        public async Task Logs_NeverContainTheBankTransferText()
        {
            _notifier.Throw = true;
            _queue.Accept = false;
            var transfer = Transfer() with { Content = "BI-MAT-NOI-DUNG", Description = "BI-MAT-NOI-DUNG" };

            await _publisher.PublishAsync(Outcome(TransferOutcomeKind.Recorded, transfer));
            await _publisher.PublishAsync(Outcome(TransferOutcomeKind.Unmatched, transfer));

            Assert.NotEmpty(_log.Entries);
            Assert.All(_log.Entries, e => Assert.DoesNotContain("BI-MAT-NOI-DUNG", e.Message));
        }
    }
}
