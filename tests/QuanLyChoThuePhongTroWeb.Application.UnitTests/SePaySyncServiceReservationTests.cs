using System;
using System.Linq;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Common.Configurations;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;
using QuanLyChoThuePhongTroWeb.Application.UnitTests.Fakes;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    // Đồng bộ bù không ghi tiền giữ chỗ (spec giữ chỗ P10, X9): dòng giữ chỗ hiện "Bỏ qua" kèm lý do, không ghi, không thông báo.
    public class SePaySyncServiceReservationTests
    {
        private const string BankAccount = "0123456789";
        private static readonly DateTime TxTime = new(2026, 10, 17, 3, 0, 0, DateTimeKind.Utc);

        [Fact]
        public async Task SyncOfAReservationTransfer_IsSkippedWithAReason_AndNothingIsRecorded()
        {
            var h = new PaymentTestHarness();
            var reservationStore = new FakeReservationAutoPaymentStore();
            var thongBao = new FakeThongBaoService();
            var queue = new RecordingPaymentNotificationQueue();
            var handler = new ReservationTransferHandler(
                reservationStore, thongBao, new OnlinePaymentOptions { ReservationEnabled = true },
                h.Time, new CapturingLogger<ReservationTransferHandler>());
            var classifier = new IncomingTransferClassifier(
                new IIncomingTransferHandler[] { new InvoiceTransferHandler(h.Transaction), handler },
                new VietQrSettings { AccountNumber = BankAccount });
            var publisher = new PaymentOutcomePublisher(
                h.Notifier, queue, new FakeTransferAlertDeduplicator(), new CapturingLogger<PaymentOutcomePublisher>());
            var ingestion = new BankTransferIngestionService(classifier, publisher, new CapturingLogger<BankTransferIngestionService>());
            var client = new FakeSePayTransactionClient();
            client.Enqueue(new[]
            {
                new SePayApiTransaction("tx-1", TxTime, BankAccount, null, "in", 1_000_000m, "Nguyen Van A TTPTGC5", "FT26ABC", null, "MBBank")
            }, hasMore: false);
            var service = new SePaySyncService(
                client, ingestion, h.Access, new SePayOptions { ApiToken = "api-token-secret", SyncRecordEnabled = true },
                h.Store, new CapturingLogger<SePaySyncService>());

            var result = await service.SyncAsync(PaymentTestHarness.AdminId,
                new SePaySyncRequest(new DateTime(2026, 10, 15, 17, 0, 0, DateTimeKind.Utc), new DateTime(2026, 10, 18, 16, 59, 59, DateTimeKind.Utc)));

            Assert.True(result.Success, result.Message);
            var row = Assert.Single(result.Data!.Rows);
            Assert.Equal(SePaySyncRowStatus.BoQua, row.TrangThai);
            Assert.Equal("Giao dịch giữ chỗ: đồng bộ bù không ghi, đối chiếu tay ở màn Giữ chỗ.", row.GhiChu);
            Assert.Empty(reservationStore.RecordCalls);
            Assert.Empty(thongBao.Sent);
            Assert.Empty(queue.Items);
            Assert.Empty(h.Store.Ledger);
        }
    }
}
