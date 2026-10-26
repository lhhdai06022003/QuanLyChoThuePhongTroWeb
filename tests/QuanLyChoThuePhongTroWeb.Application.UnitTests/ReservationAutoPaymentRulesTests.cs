using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Common.Configurations;
using QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;
using QuanLyChoThuePhongTroWeb.Application.UnitTests.Fakes;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    // Bổ sung của Tester cho ghi tiền giữ chỗ tự động (đợt 3, phần 1): thứ tự ưu tiên giữa các quy tắc X5, X8, X9, X10 và
    // đường đi đầy đủ từ webhook tới cảnh báo khi cờ tắt. Chỉ kiểm hành vi quan sát được.
    public class ReservationAutoPaymentRulesTests
    {
        private const string BankAccount = "0123456789";
        private const string Secret = "secret";
        private const string TmnCode = "TESTTMN1";
        private static readonly DateTime Now = PaymentTestHarness.Now;
        private static readonly DateTime TransferTime = new(2026, 10, 3, 2, 30, 0, DateTimeKind.Utc);

        private readonly FakeReservationAutoPaymentStore _store = new();
        private readonly FakeThongBaoService _thongBao = new();
        private readonly OnlinePaymentOptions _options = new() { ReservationEnabled = true };
        private readonly FixedTimeProvider _time = new(new DateTimeOffset(Now));

        private ReservationTransferHandler NewHandler() => new(
            _store, _thongBao, _options, _time, new CapturingLogger<ReservationTransferHandler>());

        private static IncomingBankTransfer Transfer(string content, bool sync = false, string? code = null) => new(
            92704, "MBBank", TransferTime, BankAccount, null, code, content, "in", 1_000_000m, "FT26ABC", content)
        {
            LaDongBoBu = sync
        };

        // P8: tính năng mặc định tắt, Validate() không đòi hỏi gì thêm khi cờ bật.
        [Fact]
        public void ReservationFlag_IsOffByDefault()
        {
            Assert.False(new OnlinePaymentOptions().ReservationEnabled);
        }

        // X5 + X9: đồng bộ bù được xét trước cờ. Cờ tắt mà là dòng đồng bộ vẫn là "Bỏ qua" kèm lý do, không phải "Không khớp mã".
        [Fact]
        public async Task SyncTransfer_WithTheFlagOff_IsStillIgnoredWithTheSyncReason()
        {
            _options.ReservationEnabled = false;

            var outcome = await NewHandler().HandleAsync(Transfer("TTPTGC5", sync: true), "TTPTGC5");

            Assert.Equal(TransferOutcomeKind.Ignored, outcome.Kind);
            Assert.False(string.IsNullOrWhiteSpace(outcome.LyDo));
            Assert.Empty(_store.RecordCalls);
        }

        // X5: đường đầy đủ webhook -> bộ phân loại -> handler (cờ tắt) -> publisher -> hàng đợi. Phải thành cảnh báo Telegram
        // "Không nhận ra mã thanh toán hợp lệ (TTPTGC5)" và không ghi gì, không báo nhân viên trong app.
        [Fact]
        public async Task SePayWebhook_WithTheFlagOff_RaisesTheUnmatchedAlert_AndRecordsNothing()
        {
            _options.ReservationEnabled = false;
            var h = new PaymentTestHarness();
            var queue = new RecordingPaymentNotificationQueue();
            var classifier = new IncomingTransferClassifier(
                new IIncomingTransferHandler[] { new InvoiceTransferHandler(h.Transaction), NewHandler() },
                new VietQrSettings { AccountNumber = BankAccount });
            var publisher = new PaymentOutcomePublisher(
                h.Notifier, queue, new FakeTransferAlertDeduplicator(), new CapturingLogger<PaymentOutcomePublisher>());
            var ingestion = new BankTransferIngestionService(classifier, publisher, new CapturingLogger<BankTransferIngestionService>());

            var outcome = await ingestion.IngestAsync(Transfer("Nguyen Van A TTPTGC5"));

            Assert.Equal(TransferOutcomeKind.Unmatched, outcome.Kind);
            Assert.Equal("TTPTGC5", outcome.Code);
            Assert.Empty(_store.RecordCalls);
            Assert.Empty(_thongBao.Sent);
            var item = Assert.Single(queue.Items);
            var telegram = PaymentNotificationFormatter.FormatAlertTelegram(item.Outcome, null);
            Assert.Contains("Không nhận ra mã thanh toán hợp lệ (TTPTGC5)", telegram);
        }

        // X5 (cờ bật) + P4: mã TTPTGC có nhưng không có yêu cầu tương ứng (mã cũ GC…) -> Unmatched có lý do, cũng ra cảnh báo.
        [Fact]
        public async Task SePayWebhook_ForAnUnknownReservationCode_RaisesTheUnmatchedAlert()
        {
            _store.NextResult = new Application.Features.ReservationPayments.SystemReservationRecordResult(
                Application.Features.ReservationPayments.SystemReservationRecordStatus.NotFound);
            var h = new PaymentTestHarness();
            var queue = new RecordingPaymentNotificationQueue();
            var classifier = new IncomingTransferClassifier(
                new IIncomingTransferHandler[] { new InvoiceTransferHandler(h.Transaction), NewHandler() },
                new VietQrSettings { AccountNumber = BankAccount });
            var publisher = new PaymentOutcomePublisher(
                h.Notifier, queue, new FakeTransferAlertDeduplicator(), new CapturingLogger<PaymentOutcomePublisher>());
            var ingestion = new BankTransferIngestionService(classifier, publisher, new CapturingLogger<BankTransferIngestionService>());

            var outcome = await ingestion.IngestAsync(Transfer("TTPTGC77"));

            Assert.Equal(TransferOutcomeKind.Unmatched, outcome.Kind);
            Assert.Single(queue.Items);
            Assert.Empty(_thongBao.Sent);
        }

        // X8: trường code của SePay (khác rỗng) thắng nội dung, kể cả khi nội dung chứa mã khác loại.
        [Theory]
        [InlineData("ttptgc12", "TTPTHD5 noi dung", "TTPTGC12")]
        [InlineData("TTPT GC 12", null, "TTPTGC12")]
        [InlineData("ttpthd9", "TTPTGC5", "TTPTHD9")]
        public void CodeField_WinsOverTheContent_AndIsNormalized(string code, string? content, string expected)
        {
            Assert.Equal(expected, TransferCodeParser.Extract(code, content, null));
        }

        // X8: mã hóa đơn đứng trước thì ra mã hóa đơn nên luồng hóa đơn không đổi dù nội dung có thêm mã giữ chỗ.
        [Theory]
        [InlineData("TTPTHD5TTPTGC7", "TTPTHD5")]
        [InlineData("TTPTGC7TTPTHD5", "TTPTGC7")]
        [InlineData("TTPTGC12abc", "TTPTGC12")]
        [InlineData("xTTPTGC12", "TTPTGC12")]
        public void Extract_AdjacentCodes_AreSplitByPosition(string content, string expected)
        {
            Assert.Equal(expected, TransferCodeParser.Extract(null, content, null));
        }

        [Theory]
        [InlineData("ttptgc12")]
        [InlineData("TTPTGC 12")]
        [InlineData(" TTPTGC12")]
        [InlineData("TTPTGC-12")]
        [InlineData("TTPTGC+5")]
        public void ExtractReservationId_RequiresTheExactNormalizedForm(string code)
        {
            Assert.Null(TransferCodeParser.ExtractReservationId(code));
        }

        // X10: IPN giữ chỗ - thứ tự bước. Giao dịch không thành công được ghi nhận "00" trước cả kiểm cờ và kiểm tồn tại.
        [Fact]
        public async Task Ipn_FailedPayment_IsConfirmed_EvenWhenTheFlagIsOffOrTheReservationIsUnknown()
        {
            _options.ReservationEnabled = false;
            _store.Exists = false;
            var publisher = new RecordingOutcomePublisher();
            var service = NewIpnService(publisher);

            var result = await service.HandleIpnAsync(Query(q => q["vnp_ResponseCode"] = "24"));

            Assert.Equal(VnPayIpnResult.Confirmed, result);
            Assert.Empty(_store.RecordCalls);
            Assert.Empty(_store.ExistsQueries);
            Assert.Empty(publisher.Outcomes);
        }

        // X10: cờ tắt được kiểm trước tồn tại: không hỏi store.
        [Fact]
        public async Task Ipn_FlagOff_DoesNotEvenAskTheStore()
        {
            _options.ReservationEnabled = false;
            var service = NewIpnService(new RecordingOutcomePublisher());

            var result = await service.HandleIpnAsync(Query());

            Assert.Equal(VnPayIpnResult.OrderNotFound, result);
            Assert.Empty(_store.ExistsQueries);
            Assert.Empty(_store.RecordCalls);
        }

        // X10: IPN gọi lại sau khi bật cờ thì ghi được (VNPay gọi lại tối đa 10 lần).
        [Fact]
        public async Task Ipn_RetryAfterTheFlagIsTurnedOn_IsRecorded()
        {
            _options.ReservationEnabled = false;
            var publisher = new RecordingOutcomePublisher();
            var service = NewIpnService(publisher);
            var query = Query();

            Assert.Equal(VnPayIpnResult.OrderNotFound, await service.HandleIpnAsync(query));
            _options.ReservationEnabled = true;
            Assert.Equal(VnPayIpnResult.Confirmed, await service.HandleIpnAsync(query));

            var call = Assert.Single(_store.RecordCalls);
            Assert.Equal("VNPAY-14123456", call.MaGiaoDich);
            Assert.Equal("TTPTGC5", call.MaYeuCau);
            Assert.Single(publisher.Outcomes);
        }

        // Ranh giới nhánh: txnRef kiểu hóa đơn không bao giờ được nhánh giữ chỗ nhận.
        [Theory]
        [InlineData("HD5_638000000000000000")]
        [InlineData("GC5")]
        [InlineData("GC5_")]
        [InlineData("GC5_abc")]
        [InlineData("GC-5_1")]
        public async Task Ipn_ForeignOrMalformedTxnRef_IsOrderNotFound_AndNeverRecorded(string txnRef)
        {
            var service = NewIpnService(new RecordingOutcomePublisher());

            var result = await service.HandleIpnAsync(Query(q => q["vnp_TxnRef"] = txnRef));

            Assert.Equal(VnPayIpnResult.OrderNotFound, result);
            Assert.Empty(_store.RecordCalls);
            Assert.Empty(_store.ExistsQueries);
        }

        // Số tiền: vnp_Amount lớn vẫn chia 100 đúng (không tràn, không làm tròn).
        [Fact]
        public async Task Ipn_Amount_IsDividedByOneHundredExactly()
        {
            var service = NewIpnService(new RecordingOutcomePublisher());

            var result = await service.HandleIpnAsync(Query(q => q["vnp_Amount"] = "123456700"));

            Assert.Equal(VnPayIpnResult.Confirmed, result);
            Assert.Equal(1_234_567m, Assert.Single(_store.RecordCalls).SoTien);
        }

        private ReservationVnPayService NewIpnService(IPaymentOutcomePublisher publisher) => new(
            new VnPayOptions { Enabled = true, TmnCode = TmnCode, HashSecret = Secret },
            _options, _store, NewHandler(), publisher, _time, new CapturingLogger<ReservationVnPayService>());

        private static Dictionary<string, string> Query(Action<Dictionary<string, string>>? tweak = null)
        {
            var query = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["vnp_TmnCode"] = TmnCode,
                ["vnp_Amount"] = "100000000",
                ["vnp_BankCode"] = "NCB",
                ["vnp_CardType"] = "ATM",
                ["vnp_OrderInfo"] = "Thanh toan giu cho TTPTGC5",
                ["vnp_PayDate"] = "20261003093000",
                ["vnp_ResponseCode"] = "00",
                ["vnp_TransactionNo"] = "14123456",
                ["vnp_TransactionStatus"] = "00",
                ["vnp_TxnRef"] = VnPayProtocol.BuildReservationTxnRef(5, Now)
            };
            tweak?.Invoke(query);
            query["vnp_SecureHash"] = VnPayProtocol.Sign(VnPayProtocol.BuildSignData(query), Secret);
            return query;
        }
    }
}
