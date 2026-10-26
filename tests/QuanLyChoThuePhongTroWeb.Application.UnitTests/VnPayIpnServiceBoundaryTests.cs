using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using QuanLyChoThuePhongTroWeb.Application.Common.Configurations;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Payments;
using QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;
using QuanLyChoThuePhongTroWeb.Application.UnitTests.Fakes;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    // Bổ sung cho VnPayIpnServiceTests (spec VNPay §5.2, §6, §11.1): tham số bị thiếu hẳn thay vì rỗng, TxnRef có ký tự
    // xuống dòng, thanh toán một phần, khóa chống trùng không phân biệt hoa thường/khoảng trắng, biên độ 100 ký tự của
    // khóa VNPAY-, tham số không phải vnp_* nằm ngoài chữ ký.
    public class VnPayIpnServiceBoundaryTests
    {
        private const string Secret = "secret";
        private const string TmnCode = "TESTTMN1";
        private static readonly DateTime Now = PaymentTestHarness.Now;
        private static readonly string TxnRef = VnPayProtocol.BuildTxnRef(7, Now);

        private readonly PaymentTestHarness _h = new();
        private readonly RecordingPaymentNotificationQueue _queue = new();
        private readonly VnPayPaymentService _service;

        public VnPayIpnServiceBoundaryTests()
        {
            var publisher = new PaymentOutcomePublisher(_h.Notifier, _queue, new FakeTransferAlertDeduplicator(), NullLogger<PaymentOutcomePublisher>.Instance);
            var options = new VnPayOptions { Enabled = true, TmnCode = TmnCode, HashSecret = Secret };
            _service = new VnPayPaymentService(options, new InvoiceTransferHandler(_h.Transaction), publisher, _h.Time, new CapturingLogger<VnPayPaymentService>());
        }

        private static Dictionary<string, string> Query(Action<Dictionary<string, string>>? tweak = null)
        {
            var query = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["vnp_TmnCode"] = TmnCode,
                ["vnp_Amount"] = "200000000",
                ["vnp_BankCode"] = "NCB",
                ["vnp_CardType"] = "ATM",
                ["vnp_OrderInfo"] = "Thanh toan hoa don HD-7",
                ["vnp_PayDate"] = "20261003093000",
                ["vnp_ResponseCode"] = "00",
                ["vnp_TransactionNo"] = "14123456",
                ["vnp_TransactionStatus"] = "00",
                ["vnp_TxnRef"] = TxnRef
            };
            tweak?.Invoke(query);
            query["vnp_SecureHash"] = VnPayProtocol.Sign(VnPayProtocol.BuildSignData(query), Secret);
            return query;
        }

        private void AssertNothingHappened()
        {
            Assert.Empty(_h.UnitOfWork.Transactions);
            Assert.Empty(_h.Store.Ledger);
            Assert.Empty(_h.Store.Notifications);
            Assert.Empty(_queue.Items);
        }

        // ---------- Tham số thiếu hẳn (đã ký lại nên chữ ký vẫn đúng) ----------

        [Fact]
        public async Task MissingTmnCode_SignedCorrectly_Returns97()
        {
            _h.AddSentInvoice();

            var result = await _service.HandleIpnAsync(Query(q => q.Remove("vnp_TmnCode")));

            Assert.Equal("97", result.RspCode);
            AssertNothingHappened();
        }

        [Fact]
        public async Task MissingTxnRef_SignedCorrectly_Returns01()
        {
            _h.AddSentInvoice();

            var result = await _service.HandleIpnAsync(Query(q => q.Remove("vnp_TxnRef")));

            Assert.Equal("01", result.RspCode);
            AssertNothingHappened();
        }

        [Fact]
        public async Task MissingResponseCode_IsNotASuccess_Returns00WithoutRecording()
        {
            _h.AddSentInvoice();

            var result = await _service.HandleIpnAsync(Query(q => q.Remove("vnp_ResponseCode")));

            Assert.Equal("00", result.RspCode);
            AssertNothingHappened();
        }

        [Theory]
        [InlineData("HD7_1\n")]
        [InlineData(" HD7_1")]
        [InlineData("HD7_1 ")]
        [InlineData("hd7_1")]
        [InlineData("HD1234567890_1")]
        public async Task TxnRefWithStrayCharacters_SignedCorrectly_Returns01(string txnRef)
        {
            _h.AddSentInvoice();

            var result = await _service.HandleIpnAsync(Query(q => q["vnp_TxnRef"] = txnRef));

            Assert.Equal("01", result.RspCode);
            AssertNothingHappened();
        }

        // ---------- Chữ ký ----------

        [Fact]
        public async Task UpperCaseSignature_IsAccepted_AndRecorded()
        {
            _h.AddSentInvoice();
            var query = Query();
            query["vnp_SecureHash"] = query["vnp_SecureHash"].ToUpperInvariant();

            var result = await _service.HandleIpnAsync(query);

            Assert.Equal("00", result.RspCode);
            Assert.Single(_h.Store.Ledger);
        }

        [Fact]
        public async Task ParametersOutsideTheVnpNamespace_AreNotPartOfTheSignature()
        {
            _h.AddSentInvoice();
            var query = Query();
            query["utm_source"] = "ngrok";

            var result = await _service.HandleIpnAsync(query);

            Assert.Equal("00", result.RspCode);
            Assert.Single(_h.Store.Ledger);
        }

        [Fact]
        public async Task AddingAVnpParameterAfterSigning_Returns97()
        {
            _h.AddSentInvoice();
            var query = Query();
            query["vnp_Extra"] = "1";

            var result = await _service.HandleIpnAsync(query);

            Assert.Equal("97", result.RspCode);
            AssertNothingHappened();
        }

        // ---------- Số tiền ----------

        [Fact]
        public async Task PartialPayment_RecordsExactlyWhatWasPaid_AndLeavesTheInvoicePartiallyPaid()
        {
            var hoaDon = _h.AddSentInvoice();

            var result = await _service.HandleIpnAsync(Query(q => q["vnp_Amount"] = "50000000"));

            Assert.Equal("00", result.RspCode);
            var ledger = Assert.Single(_h.Store.Ledger);
            Assert.Equal(500_000m, ledger.SoTienThanhToan);
            Assert.Null(PaymentNoteTags.Parse(ledger.GhiChu).TienThua);
            Assert.Equal(TrangThaiHoaDon.ThanhToanMotPhan, hoaDon.TrangThaiHoaDon);
            var outcome = Assert.Single(_queue.Items).Outcome;
            Assert.Equal(TransferOutcomeKind.Recorded, outcome.Kind);
            Assert.Equal(500_000m, outcome.SoTienGhiNhan);
            Assert.Equal(1_500_000m, outcome.ConLai);
        }

        [Fact]
        public async Task TwoPartialPayments_WithDifferentTransactionNumbers_BothRecorded_AndPayTheInvoiceInFull()
        {
            var hoaDon = _h.AddSentInvoice();

            var first = await _service.HandleIpnAsync(Query(q => { q["vnp_Amount"] = "50000000"; q["vnp_TransactionNo"] = "A1"; }));
            var second = await _service.HandleIpnAsync(Query(q => { q["vnp_Amount"] = "150000000"; q["vnp_TransactionNo"] = "A2"; }));

            Assert.Equal("00", first.RspCode);
            Assert.Equal("00", second.RspCode);
            Assert.Equal(new[] { "VNPAY-A1", "VNPAY-A2" }, _h.Store.Ledger.Select(l => l.MaGiaoDich).ToArray());
            Assert.Equal(TrangThaiHoaDon.DaThanhToan, hoaDon.TrangThaiHoaDon);
        }

        [Fact]
        public async Task AmountAtTheLongBoundary_ButNotDivisibleByHundred_Returns04()
        {
            _h.AddSentInvoice();

            var result = await _service.HandleIpnAsync(Query(q => q["vnp_Amount"] = "9223372036854775807"));

            Assert.Equal("04", result.RspCode);
            AssertNothingHappened();
        }

        [Fact]
        public async Task AmountWithSurroundingSpaces_Returns04()
        {
            _h.AddSentInvoice();

            var result = await _service.HandleIpnAsync(Query(q => q["vnp_Amount"] = " 200000000"));

            Assert.Equal("04", result.RspCode);
            AssertNothingHappened();
        }

        // ---------- Khóa chống trùng ----------

        [Fact]
        public async Task SameTransactionNumber_WithDifferentCaseAndSpaces_IsTheSameKey_Returns02()
        {
            _h.AddSentInvoice();
            await _service.HandleIpnAsync(Query(q => q["vnp_TransactionNo"] = "ab12cd"));

            var result = await _service.HandleIpnAsync(Query(q => q["vnp_TransactionNo"] = "AB12CD"));

            Assert.Equal("02", result.RspCode);
            Assert.Equal("VNPAY-AB12CD", Assert.Single(_h.Store.Ledger).MaGiaoDich);
            Assert.Single(_queue.Items);
        }

        [Fact]
        public async Task TransactionNumberThatMakesTheKeyExactly100Characters_IsRecorded()
        {
            _h.AddSentInvoice();
            var transactionNo = new string('7', 94);

            var result = await _service.HandleIpnAsync(Query(q => q["vnp_TransactionNo"] = transactionNo));

            Assert.Equal("00", result.RspCode);
            var ledger = Assert.Single(_h.Store.Ledger);
            Assert.Equal(InvoicePaymentPolicy.MaxMaGiaoDichLength, ledger.MaGiaoDich.Length);
        }

        [Fact]
        public async Task TransactionNumberThatMakesTheKey101Characters_Returns99()
        {
            _h.AddSentInvoice();

            var result = await _service.HandleIpnAsync(Query(q => q["vnp_TransactionNo"] = new string('7', 95)));

            Assert.Equal("99", result.RspCode);
            AssertNothingHappened();
        }

        [Fact]
        public async Task FailedPayment_ThenSuccessfulOneForTheSameInvoice_OnlyTheSuccessIsRecorded()
        {
            _h.AddSentInvoice();

            var failed = await _service.HandleIpnAsync(Query(q => { q["vnp_ResponseCode"] = "24"; q["vnp_TransactionNo"] = "0"; }));
            var success = await _service.HandleIpnAsync(Query());

            Assert.Equal("00", failed.RspCode);
            Assert.Equal("00", success.RspCode);
            Assert.Equal("VNPAY-14123456", Assert.Single(_h.Store.Ledger).MaGiaoDich);
        }

        // ---------- Hóa đơn đã thanh toán qua kênh khác ----------

        [Fact]
        public async Task InvoicePaidInFullBySePay_ThenVnPayIpn_Returns02_NoLedgerRow_AndARefundAlert()
        {
            _h.AddSentInvoice();
            _h.AddSePayLedger(7, 2_000_000m, "FT-FULL", Now.AddHours(-1));

            var result = await _service.HandleIpnAsync(Query());

            Assert.Equal("02", result.RspCode);
            Assert.Single(_h.Store.Ledger);
            var outcome = Assert.Single(_queue.Items).Outcome;
            Assert.Equal(TransferOutcomeKind.AlreadyPaid, outcome.Kind);
            Assert.Equal(OnlinePaymentChannel.VnPay, outcome.Transfer.Kenh);
        }
    }
}
