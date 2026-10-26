using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Notifications;
using QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;
using QuanLyChoThuePhongTroWeb.Application.UnitTests.Fakes;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    // Sự kiện SignalR cho khách và hàng đợi Telegram + email khi tiền giữ chỗ được ghi tự động (spec giữ chỗ §8).
    public class PaymentOutcomePublisherReservationTests
    {
        private static readonly DateTime TransferTime = new(2026, 10, 3, 2, 30, 0, DateTimeKind.Utc);

        private sealed class ReservationNotifier : IThongBaoNotifier
        {
            public List<(int UserId, ReservationPaymentUpdatedEvent Payload)> ReservationUpdates { get; } = new();
            public int InvoiceUpdateCalls { get; private set; }
            public bool Throw { get; set; }

            public Task SendToUserAsync(int nguoiDungId, object payload, CancellationToken cancellationToken = default) => Task.CompletedTask;

            public Task SendToRoleGroupAsync(string roleName, object payload, CancellationToken cancellationToken = default) => Task.CompletedTask;

            public Task SendInvoicePaymentUpdatedAsync(int nguoiDungId, InvoicePaymentUpdatedEvent payload, CancellationToken cancellationToken = default)
            {
                InvoiceUpdateCalls++;
                return Task.CompletedTask;
            }

            public Task SendReservationPaymentUpdatedAsync(int nguoiDungId, ReservationPaymentUpdatedEvent payload, CancellationToken cancellationToken = default)
            {
                if (Throw)
                {
                    throw new InvalidOperationException("SignalR down");
                }

                ReservationUpdates.Add((nguoiDungId, payload));
                return Task.CompletedTask;
            }
        }

        private readonly ReservationNotifier _notifier = new();
        private readonly RecordingPaymentNotificationQueue _queue = new();
        private readonly CapturingLogger<PaymentOutcomePublisher> _log = new();
        private readonly PaymentOutcomePublisher _publisher;

        public PaymentOutcomePublisherReservationTests()
        {
            _publisher = new PaymentOutcomePublisher(_notifier, _queue, new FakeTransferAlertDeduplicator(), _log);
        }

        private static TransferOutcome Outcome(decimal daNhanSau = 1_000_000m, decimal conLai = 0m, string trangThai = "DangGiuCho") => new()
        {
            Kind = TransferOutcomeKind.Recorded,
            Transfer = new IncomingBankTransfer(92704, "MBBank", TransferTime, "0123456789", null, null, "TTPTGC5", "in", 1_000_000m, "FT26ABC", "TTPTGC5"),
            Code = "TTPTGC5",
            SoTienGhiNhan = 1_000_000m,
            TongTien = 1_000_000m,
            DaThu = daNhanSau,
            ConLai = conLai,
            GiuCho = FakeReservationAutoPaymentStore.Recorded(daNhanSau: daNhanSau, trangThai: trangThai)
        };

        [Fact]
        public async Task Recorded_SendsTheReservationEvent_ToTheGuest_AndQueuesTheWork()
        {
            var outcome = Outcome(daNhanSau: 400_000m, conLai: 600_000m, trangThai: "ChoXacNhanTien");

            await _publisher.PublishAsync(outcome);

            var update = Assert.Single(_notifier.ReservationUpdates);
            Assert.Equal(700, update.UserId);
            Assert.Equal(new ReservationPaymentUpdatedEvent(5, 400_000m, 600_000m, "ChoXacNhanTien"), update.Payload);
            Assert.Equal(0, _notifier.InvoiceUpdateCalls);
            Assert.Same(outcome, Assert.Single(_queue.Items).Outcome);
            Assert.Empty(_log.Entries);
        }

        [Fact]
        public async Task Recorded_WhenTheEventFails_LogsAWarning_AndStillQueuesTheWork()
        {
            _notifier.Throw = true;

            await _publisher.PublishAsync(Outcome());

            Assert.Single(_queue.Items);
            var entry = Assert.Single(_log.Entries);
            Assert.Equal(LogLevel.Warning, entry.Level);
            Assert.Contains("SEPAY-FT26ABC", entry.Message);
            Assert.Contains("5", entry.Message);
        }

        [Fact]
        public async Task Recorded_ForAnInvoice_SendsNoReservationEvent()
        {
            var outcome = Outcome() with { GiuCho = null, HoaDonId = 7, TenantUserId = 500, LichSuThanhToanId = 77 };

            await _publisher.PublishAsync(outcome);

            Assert.Empty(_notifier.ReservationUpdates);
            Assert.Equal(1, _notifier.InvoiceUpdateCalls);
            Assert.Single(_queue.Items);
        }
    }
}
