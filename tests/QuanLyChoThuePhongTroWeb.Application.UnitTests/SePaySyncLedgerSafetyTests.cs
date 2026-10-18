using QuanLyChoThuePhongTroWeb.Application.Common.Configurations;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;
using QuanLyChoThuePhongTroWeb.Application.UnitTests.Fakes;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    // Đồng bộ bù chạy qua ghi sổ THẬT (bộ phân loại, InvoiceTransferHandler, khung giao dịch, publisher) với kho giả:
    // kiểm hậu quả lên sổ và thông báo, không kiểm ruột bên trong. Bổ sung cho SePaySyncServiceTests (ingest giả).
    public class SePaySyncLedgerSafetyTests
    {
        private const int AdminId = PaymentTestHarness.AdminId;
        private const string BankAccount = "0123456789";

        private static readonly DateTime From = new(2026, 10, 15, 17, 0, 0, DateTimeKind.Utc);
        private static readonly DateTime To = new(2026, 10, 18, 16, 59, 59, DateTimeKind.Utc);
        private static readonly DateTime TxTime = new(2026, 10, 17, 3, 0, 0, DateTimeKind.Utc);

        private readonly FakeSePayTransactionClient _client = new();
        private readonly SePayOptions _options = new() { ApiToken = "api-token-secret", SyncRecordEnabled = true };
        private readonly PaymentTestHarness _h = new();
        private readonly RecordingPaymentNotificationQueue _queue = new();
        private readonly FakeTransferAlertDeduplicator _dedup = new();
        private readonly BankTransferIngestionService _ingestion;

        public SePaySyncLedgerSafetyTests()
        {
            _h.AddSentInvoice();
            var publisher = new PaymentOutcomePublisher(_h.Notifier, _queue, _dedup, new CapturingLogger<PaymentOutcomePublisher>());
            var classifier = new IncomingTransferClassifier(
                new IIncomingTransferHandler[] { new InvoiceTransferHandler(_h.Transaction) }, new VietQrSettings { AccountNumber = BankAccount });
            _ingestion = new BankTransferIngestionService(classifier, publisher, new CapturingLogger<BankTransferIngestionService>());
        }

        private SePaySyncService Service() => new(_client, _ingestion, _h.Access, _options, _h.Store, new CapturingLogger<SePaySyncService>());

        private static SePayApiTransaction Tx(
            string id, string? reference, decimal amount = 2_000_000m, string content = "Nguyen Van A TTPTHD7", string? type = "in")
            => new(id, TxTime, BankAccount, null, type, amount, content, reference, null, "MBBank");

        // Giao dịch như webhook gửi tới: có id số nguyên, cùng mã tham chiếu.
        private static IncomingBankTransfer Webhook(long sePayId, string? reference, decimal amount = 2_000_000m, string content = "TTPTHD7")
            => new(sePayId, "MBBank", TxTime, BankAccount, null, null, content, "in", amount, reference, null);

        private async Task<SePaySyncResult> SyncAsync(params SePayApiTransaction[] items)
        {
            _client.Enqueue(items, hasMore: false);
            var result = await Service().SyncAsync(AdminId, new SePaySyncRequest(From, To));
            Assert.True(result.Success, result.Message);
            return result.Data!;
        }

        // ---------- SyncRecordEnabled = false: tuyệt đối không ghi sổ, không thông báo ----------

        [Fact]
        public async Task CheckOnlyMode_WritesNoLedgerRow_ChangesNoInvoice_AndSendsNoNotification()
        {
            _options.SyncRecordEnabled = false;
            var invoiceBefore = _h.Store.Invoices[7].TrangThaiHoaDon;

            var data = await SyncAsync(Tx("a", "FT-NEW"), Tx("b", "FT-NEW-2", content: "khong co ma"), Tx("c", "FT-GHOST", content: "TTPTHD999"));

            Assert.All(data.Rows, r => Assert.Equal(SePaySyncRowStatus.ChuaGhiSo, r.TrangThai));
            Assert.Empty(_h.Store.Ledger);
            Assert.Equal(invoiceBefore, _h.Store.Invoices[7].TrangThaiHoaDon);
            Assert.Empty(_h.UnitOfWork.Transactions);
            Assert.Equal(0, _h.UnitOfWork.SaveChangesCalls);
            Assert.Empty(_queue.Items);
            Assert.Empty(_h.Notifier.Sent);
            Assert.Empty(_h.Notifier.PaymentUpdates);
            Assert.Equal(0, _dedup.Calls);
        }

        [Fact]
        public async Task CheckOnlyMode_RunTwice_StillWritesNothing()
        {
            _options.SyncRecordEnabled = false;

            await SyncAsync(Tx("a", "FT-NEW"));
            await SyncAsync(Tx("a", "FT-NEW"));

            Assert.Empty(_h.Store.Ledger);
            Assert.Empty(_queue.Items);
        }

        [Fact]
        public async Task TurningTheFlagOn_ThenRunningAgain_RecordsOnce_AndTheCheckOnlyRunAfterwardsReportsItExists()
        {
            _options.SyncRecordEnabled = false;
            var before = await SyncAsync(Tx("a", "FT26ABC"));
            _options.SyncRecordEnabled = true;
            var recorded = await SyncAsync(Tx("a", "FT26ABC"));
            _options.SyncRecordEnabled = false;
            var after = await SyncAsync(Tx("a", "FT26ABC"));

            Assert.Equal(SePaySyncRowStatus.ChuaGhiSo, Assert.Single(before.Rows).TrangThai);
            Assert.Equal(SePaySyncRowStatus.MoiGhiNhan, Assert.Single(recorded.Rows).TrangThai);
            Assert.Equal(SePaySyncRowStatus.DaCo, Assert.Single(after.Rows).TrangThai);
            Assert.Equal("SEPAY-FT26ABC", Assert.Single(_h.Store.Ledger).MaGiaoDich);
        }

        // ---------- Chống ghi đôi giữa webhook và đồng bộ bù ----------

        [Fact]
        public async Task SyncThenWebhook_WithTheSameReference_RecordsOnce_AndTheWebhookIsADuplicate()
        {
            await SyncAsync(Tx("api-1", "FT26ABC"));

            var webhook = await _ingestion.IngestAsync(Webhook(99001, " ft26abc"));

            Assert.Equal(TransferOutcomeKind.Duplicate, webhook.Kind);
            Assert.Single(_h.Store.Ledger);
            Assert.Equal(TrangThaiHoaDon.DaThanhToan, _h.Store.Invoices[7].TrangThaiHoaDon);
            // Chỉ lần ghi đầu tiên (từ đồng bộ bù) phát thông báo; webhook trùng không phát lại.
            Assert.Single(_queue.Items);
        }

        [Fact]
        public async Task WebhookThenSync_WithTheSameReference_RecordsOnce_AndSyncSendsNothingMore()
        {
            var webhook = await _ingestion.IngestAsync(Webhook(99001, "FT26ABC"));
            Assert.Equal(TransferOutcomeKind.Recorded, webhook.Kind);
            var queuedAfterWebhook = _queue.Items.Count;
            var paymentUpdatesAfterWebhook = _h.Notifier.PaymentUpdates.Count;

            var data = await SyncAsync(Tx("api-1", "ft26abc "));

            Assert.Equal(SePaySyncRowStatus.DaCo, Assert.Single(data.Rows).TrangThai);
            Assert.Single(_h.Store.Ledger);
            Assert.Equal(queuedAfterWebhook, _queue.Items.Count);
            Assert.Equal(paymentUpdatesAfterWebhook, _h.Notifier.PaymentUpdates.Count);
        }

        [Fact]
        public async Task TwoApiIdsWithTheSameReference_InOneRun_RecordOnce()
        {
            var data = await SyncAsync(Tx("api-1", "FT26ABC"), Tx("api-2", "FT26ABC"));

            Assert.Equal(new[] { SePaySyncRowStatus.MoiGhiNhan, SePaySyncRowStatus.DaCo }, data.Rows.Select(r => r.TrangThai));
            Assert.Single(_h.Store.Ledger);
        }

        [Fact]
        public async Task APartialTransfer_SyncedTwice_IsNotAddedTwice()
        {
            var first = await SyncAsync(Tx("a", "FT-PART", amount: 500_000m));
            var second = await SyncAsync(Tx("a", "FT-PART", amount: 500_000m));

            Assert.Equal(SePaySyncRowStatus.MoiGhiNhan, Assert.Single(first.Rows).TrangThai);
            Assert.Equal("Ghi 500.000đ, còn lại 1.500.000đ", first.Rows[0].GhiChu);
            Assert.Equal(SePaySyncRowStatus.DaCo, Assert.Single(second.Rows).TrangThai);
            Assert.Equal(500_000m, Assert.Single(_h.Store.Ledger).SoTienThanhToan);
            Assert.Equal(TrangThaiHoaDon.ThanhToanMotPhan, _h.Store.Invoices[7].TrangThaiHoaDon);
        }

        [Fact]
        public async Task ATransferThatTheWebhookRecordedAndAnAdminVoided_StaysVoided_InBothModes()
        {
            _h.AddSePayLedger(7, 2_000_000m, "FT26ABC", PaymentTestHarness.Now.AddDays(-1), isDeleted: true);

            var recordMode = await SyncAsync(Tx("a", "FT26ABC"));
            _options.SyncRecordEnabled = false;
            var checkOnly = await SyncAsync(Tx("a", "FT26ABC"));

            Assert.Equal(SePaySyncRowStatus.DaCo, Assert.Single(recordMode.Rows).TrangThai);
            Assert.Equal(SePaySyncRowStatus.DaCo, Assert.Single(checkOnly.Rows).TrangThai);
            Assert.True(Assert.Single(_h.Store.Ledger).IsDeleted);
        }

        // ---------- Không ghi sổ ----------

        [Fact]
        public async Task ACancelledInvoice_IsNeverCredited_AndIsLabelledAsNotAcceptingMoney()
        {
            var invoice = _h.Store.Invoices[7];
            invoice.TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaHuy;
            invoice.IsDeleted = true;

            var row = Assert.Single((await SyncAsync(Tx("a", "FT-CANCELLED"))).Rows);

            Assert.Equal(SePaySyncRowStatus.HoaDonKhongNhanTien, row.TrangThai);
            Assert.Equal("Hóa đơn đã hủy", row.GhiChu);
            Assert.Empty(_h.Store.Ledger);
        }

        [Fact]
        public async Task AFullyPaidInvoice_IsNotCreditedAgain()
        {
            _h.AddLedger(7, 2_000_000m);

            var row = Assert.Single((await SyncAsync(Tx("a", "FT-LATE"))).Rows);

            Assert.Equal(SePaySyncRowStatus.HoaDonKhongNhanTien, row.TrangThai);
            Assert.Equal("Hóa đơn đã thu đủ", row.GhiChu);
            Assert.Single(_h.Store.Ledger);
        }

        [Fact]
        public async Task OutgoingMoneyAndMissingReferences_NeverReachTheLedger_EvenWhenTheContentNamesAnInvoice()
        {
            var data = await SyncAsync(Tx("a", "FT-OUT", type: "out"), Tx("b", null), Tx("c", "  "));

            Assert.Equal(
                new[] { SePaySyncRowStatus.BoQua, SePaySyncRowStatus.ThieuMaThamChieu, SePaySyncRowStatus.ThieuMaThamChieu },
                data.Rows.Select(r => r.TrangThai));
            Assert.Empty(_h.Store.Ledger);
            Assert.Empty(_queue.Items);
        }

        // ---------- Phân quyền: không phải Admin thì không ghi gì ----------

        [Theory]
        [InlineData(PaymentTestHarness.StaffId)]
        [InlineData(PaymentTestHarness.OtherStaffId)]
        [InlineData(0)]
        [InlineData(-7)]
        public async Task NonAdminActors_WriteNothing_AndNeverCallTheClient(int actorId)
        {
            _client.Enqueue(new[] { Tx("a", "FT-STAFF") }, hasMore: false);

            var result = await Service().SyncAsync(actorId, new SePaySyncRequest(From, To));

            Assert.Equal(ServiceErrorKind.Forbidden, result.ErrorKind);
            Assert.Empty(_client.Calls);
            Assert.Empty(_h.Store.Ledger);
            Assert.Empty(_queue.Items);
        }
    }
}
