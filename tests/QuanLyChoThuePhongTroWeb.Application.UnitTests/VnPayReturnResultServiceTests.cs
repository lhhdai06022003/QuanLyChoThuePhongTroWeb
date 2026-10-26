using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using QuanLyChoThuePhongTroWeb.Application.Common.Configurations;
using QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;
using QuanLyChoThuePhongTroWeb.Application.UnitTests.Fakes;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    // Trang kết quả VNPay (spec VNPay §4.3): chỉ đọc, không lộ dữ liệu hóa đơn khi không xác thực được.
    public class VnPayReturnResultServiceTests
    {
        private const string Secret = "secret";
        private const string TmnCode = "TESTTMN1";
        private const string TransactionNo = "14123456";
        private static readonly DateTime Now = PaymentTestHarness.Now;
        private static readonly string TxnRef = VnPayProtocol.BuildTxnRef(7, Now);

        private readonly PaymentTestHarness _h = new();
        private readonly VnPayPaymentService _service;

        public VnPayReturnResultServiceTests()
        {
            var options = new VnPayOptions { Enabled = true, TmnCode = TmnCode, HashSecret = Secret };
            var publisher = new PaymentOutcomePublisher(_h.Notifier, new RecordingPaymentNotificationQueue(), new FakeTransferAlertDeduplicator(), NullLogger<PaymentOutcomePublisher>.Instance);
            _service = new VnPayPaymentService(options, _h.Store, new InvoiceTransferHandler(_h.Transaction), publisher, _h.Time, NullLogger<VnPayPaymentService>.Instance);
        }

        // Query trả về hợp lệ của hóa đơn 7 (2.000.000đ); tweak sửa tham số trước khi ký.
        private static Dictionary<string, string> Query(Action<Dictionary<string, string>>? tweak = null, bool sign = true)
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
                ["vnp_TransactionNo"] = TransactionNo,
                ["vnp_TransactionStatus"] = "00",
                ["vnp_TxnRef"] = TxnRef
            };
            tweak?.Invoke(query);
            if (sign)
            {
                query["vnp_SecureHash"] = VnPayProtocol.Sign(VnPayProtocol.BuildSignData(query), Secret);
            }

            return query;
        }

        private Task<VnPayReturnResult> ReturnAsync(Dictionary<string, string> query, int nguoiThueId = PaymentTestHarness.TenantId)
            => _service.GetReturnResultAsync(query, nguoiThueId);

        private static void AssertUnverified(VnPayReturnResult result)
        {
            Assert.Equal(VnPayReturnStatus.KhongXacThuc, result.TrangThai);
            Assert.Null(result.HoaDonId);
            Assert.Null(result.SoTien);
            Assert.Null(result.SoTienVnPayBao);
            Assert.Null(result.LichSuThanhToanId);
            Assert.Null(result.MoTaLoi);
        }

        private void AssertNothingWritten()
        {
            Assert.Equal(0, _h.Store.LockCalls);
            Assert.Empty(_h.UnitOfWork.Transactions);
            Assert.Equal(0, _h.UnitOfWork.SaveChangesCalls);
            Assert.Empty(_h.Store.Notifications);
        }

        // ---------- Không xác thực được ----------

        [Fact]
        public async Task MissingSignature_IsUnverified()
        {
            _h.AddSentInvoice();

            AssertUnverified(await ReturnAsync(Query(sign: false)));
        }

        [Fact]
        public async Task WrongSignature_IsUnverified()
        {
            _h.AddSentInvoice();
            var query = Query();
            var signature = query["vnp_SecureHash"];
            query["vnp_SecureHash"] = (signature[0] == '0' ? '1' : '0') + signature[1..];

            AssertUnverified(await ReturnAsync(query));
        }

        [Fact]
        public async Task AmountChangedAfterSigning_IsUnverified()
        {
            _h.AddSentInvoice();
            var query = Query();
            query["vnp_Amount"] = "100";

            AssertUnverified(await ReturnAsync(query));
        }

        [Fact]
        public async Task OtherMerchantCode_IsUnverified()
        {
            _h.AddSentInvoice();

            AssertUnverified(await ReturnAsync(Query(q => q["vnp_TmnCode"] = "OTHER")));
        }

        [Fact]
        public async Task MalformedOrderReference_IsUnverified()
        {
            _h.AddSentInvoice();

            AssertUnverified(await ReturnAsync(Query(q => q["vnp_TxnRef"] = "ABC")));
        }

        [Fact]
        public async Task InvoiceOfAnotherTenant_IsUnverified()
        {
            _h.AddSentInvoice();

            AssertUnverified(await ReturnAsync(Query(), PaymentTestHarness.TenantId + 1));
        }

        [Fact]
        public async Task UnknownInvoice_IsUnverified()
        {
            AssertUnverified(await ReturnAsync(Query()));
        }

        [Fact]
        public async Task MissingResponseCode_IsUnverified()
        {
            _h.AddSentInvoice();

            AssertUnverified(await ReturnAsync(Query(q => q.Remove("vnp_ResponseCode"))));
        }

        [Fact]
        public async Task SuccessWithoutATransactionNumber_IsUnverified()
        {
            _h.AddSentInvoice();

            AssertUnverified(await ReturnAsync(Query(q => q.Remove("vnp_TransactionNo"))));
        }

        [Fact]
        public async Task SuccessWithATransactionNumberTooLongForTheKey_IsUnverified()
        {
            _h.AddSentInvoice();

            AssertUnverified(await ReturnAsync(Query(q => q["vnp_TransactionNo"] = new string('9', 95))));
        }

        // ---------- Không thành công ----------

        [Fact]
        public async Task CustomerCancelled_IsDaHuy()
        {
            _h.AddSentInvoice();

            var result = await ReturnAsync(Query(q => q["vnp_ResponseCode"] = "24"));

            Assert.Equal(VnPayReturnStatus.DaHuy, result.TrangThai);
            Assert.Equal(7, result.HoaDonId);
            Assert.Null(result.SoTien);
        }

        [Fact]
        public async Task OtherFailureCode_IsKhongThanhCong_WithTheTableDescription()
        {
            _h.AddSentInvoice();

            var result = await ReturnAsync(Query(q => q["vnp_ResponseCode"] = "51"));

            Assert.Equal(VnPayReturnStatus.KhongThanhCong, result.TrangThai);
            Assert.Equal(7, result.HoaDonId);
            Assert.Equal("Tài khoản không đủ số dư để thực hiện giao dịch.", result.MoTaLoi);
        }

        [Fact]
        public async Task UnknownFailureCode_ShowsTheCode()
        {
            _h.AddSentInvoice();

            var result = await ReturnAsync(Query(q => q["vnp_ResponseCode"] = "42"));

            Assert.Equal(VnPayReturnStatus.KhongThanhCong, result.TrangThai);
            Assert.Equal("Giao dịch không thành công (mã 42)", result.MoTaLoi);
        }

        [Fact]
        public async Task Code07_ShowsTheSuspiciousTransactionDescription()
        {
            _h.AddSentInvoice();

            var result = await ReturnAsync(Query(q => q["vnp_ResponseCode"] = "07"));

            Assert.Equal(VnPayReturnStatus.KhongThanhCong, result.TrangThai);
            Assert.Contains("mã 07", result.MoTaLoi);
            Assert.Contains("liên hệ chủ trọ", result.MoTaLoi);
        }

        // ---------- Thành công (00) ----------

        [Fact]
        public async Task Success_WithTheLedgerRowOfThisTransaction_IsDaGhiNhan()
        {
            _h.AddSentInvoice();
            var ledger = _h.AddVnPayLedger(7, 2_000_000m, TransactionNo, Now);

            var result = await ReturnAsync(Query());

            Assert.Equal(VnPayReturnStatus.DaGhiNhan, result.TrangThai);
            Assert.Equal(7, result.HoaDonId);
            Assert.Equal(ledger.LichSuThanhToanId, result.LichSuThanhToanId);
            Assert.Equal(2_000_000m, result.SoTien);
            Assert.Null(result.SoTienVnPayBao);
            AssertNothingWritten();
        }

        [Fact]
        public async Task Success_TheTransactionNumberMatchesIgnoringCaseAndWhitespace()
        {
            _h.AddSentInvoice();
            _h.AddVnPayLedger(7, 2_000_000m, "vnp 1234ab", Now);

            var result = await ReturnAsync(Query(q => q["vnp_TransactionNo"] = " VNP1234AB "));

            Assert.Equal(VnPayReturnStatus.DaGhiNhan, result.TrangThai);
        }

        [Fact]
        public async Task Success_WhenTheRecordedAmountIsLessThanWhatVnPayReports_ShowsBoth()
        {
            // Khách vừa chuyển khoản QR 500.000đ nên sổ chỉ ghi phần còn nợ 1.500.000đ, còn VNPay báo 2.000.000đ.
            _h.AddSentInvoice();
            _h.AddLedger(7, 500_000m);
            _h.AddVnPayLedger(7, 1_500_000m, TransactionNo, Now);

            var result = await ReturnAsync(Query());

            Assert.Equal(VnPayReturnStatus.DaGhiNhan, result.TrangThai);
            Assert.Equal(1_500_000m, result.SoTien);
            Assert.Equal(2_000_000m, result.SoTienVnPayBao);
        }

        [Fact]
        public async Task Success_WithAnotherTransactionRowOnly_IsDangGhiNhan()
        {
            _h.AddSentInvoice();
            _h.AddVnPayLedger(7, 500_000m, "99999999", Now);

            var result = await ReturnAsync(Query());

            Assert.Equal(VnPayReturnStatus.DangGhiNhan, result.TrangThai);
            Assert.Equal(7, result.HoaDonId);
            Assert.Null(result.SoTien);
            Assert.Null(result.LichSuThanhToanId);
            AssertNothingWritten();
        }

        [Fact]
        public async Task Success_BeforeTheIpn_IsDangGhiNhan_AndWritesNothing()
        {
            _h.AddSentInvoice();

            var result = await ReturnAsync(Query());

            Assert.Equal(VnPayReturnStatus.DangGhiNhan, result.TrangThai);
            Assert.Empty(_h.Store.Ledger);
            AssertNothingWritten();
        }

        [Fact]
        public async Task Success_WhenTheInvoiceWasPaidByOtherMeans_IsKhongGhiNhan()
        {
            // Kịch bản V03: SePay đã trả đủ trước khi IPN VNPay tới; IPN trả 02 và không ghi.
            _h.AddSentInvoice();
            _h.AddSePayLedger(7, 2_000_000m, "FT26ABC", Now);

            var result = await ReturnAsync(Query());

            Assert.Equal(VnPayReturnStatus.KhongGhiNhan, result.TrangThai);
            Assert.Equal(7, result.HoaDonId);
            Assert.Null(result.SoTien);
            Assert.Null(result.LichSuThanhToanId);
        }

        [Fact]
        public async Task Success_WhenTheInvoiceIsNoLongerSent_IsKhongGhiNhan()
        {
            _h.AddSentInvoice().TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaHuy;

            var result = await ReturnAsync(Query());

            Assert.Equal(VnPayReturnStatus.KhongGhiNhan, result.TrangThai);
        }

        [Fact]
        public async Task Success_WhenTheSoftDeletedRowOfThisTransactionExists_IsKhongGhiNhan()
        {
            // Admin đã hủy ghi nhận dòng VNPAY-{txn}: IPN gọi lại cũng không ghi lại.
            _h.AddSentInvoice();
            _h.AddVnPayLedger(7, 2_000_000m, TransactionNo, Now, isDeleted: true);

            var result = await ReturnAsync(Query());

            Assert.Equal(VnPayReturnStatus.KhongGhiNhan, result.TrangThai);
            Assert.Null(result.SoTien);
            AssertNothingWritten();
        }

        [Fact]
        public async Task Success_WhenTheIpnCommitsBetweenTheLedgerAndInvoiceReads_IsDaGhiNhan()
        {
            // IPN ghi đủ tiền ngay sau lần đọc sổ đầu: hóa đơn đã hết nợ nhưng dòng của chính giao dịch này đã có.
            _h.AddSentInvoice();
            LichSuThanhToan? ledger = null;
            _h.Store.OnInvoiceSnapshot = () => ledger ??= _h.AddVnPayLedger(7, 2_000_000m, TransactionNo, Now);

            var result = await ReturnAsync(Query());

            Assert.Equal(VnPayReturnStatus.DaGhiNhan, result.TrangThai);
            Assert.Equal(ledger!.LichSuThanhToanId, result.LichSuThanhToanId);
            Assert.Equal(2_000_000m, result.SoTien);
        }

        [Fact]
        public async Task Success_WhenOnlyAnotherSoftDeletedRowExists_IsStillDangGhiNhan()
        {
            _h.AddSentInvoice();
            _h.AddVnPayLedger(7, 2_000_000m, "99999999", Now, isDeleted: true);

            var result = await ReturnAsync(Query());

            Assert.Equal(VnPayReturnStatus.DangGhiNhan, result.TrangThai);
        }
    }
}
