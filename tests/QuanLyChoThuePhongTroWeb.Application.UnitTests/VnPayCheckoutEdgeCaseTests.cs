using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using QuanLyChoThuePhongTroWeb.Application.Common.Configurations;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;
using QuanLyChoThuePhongTroWeb.Application.UnitTests.Fakes;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    // Kiểm bổ sung (tester) cho VNPay phần 2: các ca biên của tạo link (spec VNPay §4.2, §5.1), trang kết quả (§4.3)
    // và log không rò dữ liệu. Bổ sung cho VnPayCheckoutServiceTests và VnPayReturnResultServiceTests, không thay thế.
    public class VnPayCheckoutEdgeCaseTests
    {
        private const string Secret = "secret";
        private const string TmnCode = "TESTTMN1";
        private const string ReturnUrl = "http://localhost:5009/KhachThue/HoaDon/ThanhToan/VnPay/KetQua";
        private const string TransactionNo = "14123456";
        private static readonly DateTime Now = PaymentTestHarness.Now;
        private static readonly string TxnRef = VnPayProtocol.BuildTxnRef(7, Now);

        private readonly PaymentTestHarness _h = new();
        private readonly CapturingLogger<VnPayPaymentService> _log = new();

        private VnPayPaymentService Create(string tmnCode = TmnCode)
        {
            var options = new VnPayOptions { Enabled = true, TmnCode = tmnCode, HashSecret = Secret };
            var publisher = new PaymentOutcomePublisher(_h.Notifier, new RecordingPaymentNotificationQueue(), new FakeTransferAlertDeduplicator(), NullLogger<PaymentOutcomePublisher>.Instance);
            return new VnPayPaymentService(options, _h.Store, new InvoiceTransferHandler(_h.Transaction), publisher, _h.Time, _log);
        }

        private Task<ServiceResult<string>> CreateAsync(string returnUrl = ReturnUrl, string tmnCode = TmnCode)
            => Create(tmnCode).CreatePaymentUrlAsync(7, PaymentTestHarness.TenantId, "203.0.113.9", returnUrl);

        private static Dictionary<string, string> ParseQuery(string url)
        {
            return url[(url.IndexOf('?') + 1)..].Split('&')
                .Select(pair => pair.Split('=', 2))
                .ToDictionary(p => p[0], p => WebUtility.UrlDecode(p[1]), StringComparer.Ordinal);
        }

        private static Dictionary<string, string> ReturnQuery(Action<Dictionary<string, string>>? tweak = null, bool sign = true)
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
            => Create().GetReturnResultAsync(query, nguoiThueId);

        private static void AssertUnverified(VnPayReturnResult result)
        {
            Assert.Equal(VnPayReturnStatus.KhongXacThuc, result.TrangThai);
            Assert.Null(result.HoaDonId);
            Assert.Null(result.SoTien);
            Assert.Null(result.SoTienVnPayBao);
            Assert.Null(result.LichSuThanhToanId);
            Assert.Null(result.MoTaLoi);
        }

        // ---------- Tạo link: số tiền ----------

        [Theory]
        [InlineData(1_234_567, "123456700")]
        [InlineData(1, "100")]
        [InlineData(999_999_999, "99999999900")]
        public async Task Amount_IsTheRemainingBalanceTimes100_ForAnyWholeAmount(int tongTien, string expected)
        {
            _h.AddSentInvoice(tongTien: tongTien);

            var result = await CreateAsync();

            Assert.True(result.Success);
            Assert.Equal(expected, ParseQuery(result.Data!)["vnp_Amount"]);
        }

        [Fact]
        public async Task SoftDeletedLedgerRows_DoNotReduceTheAmount()
        {
            _h.AddSentInvoice();
            _h.AddLedger(7, 500_000m, "GD-1");
            _h.AddLedger(7, 900_000m, "GD-2", isDeleted: true);

            var result = await CreateAsync();

            Assert.Equal("150000000", ParseQuery(result.Data!)["vnp_Amount"]);
        }

        [Fact]
        public async Task OverpaidInvoice_IsRefusedAsFullyPaid()
        {
            _h.AddSentInvoice();
            _h.AddLedger(7, 2_500_000m);

            var result = await CreateAsync();

            Assert.False(result.Success);
            Assert.Contains("đã được thanh toán đủ", result.Message);
        }

        [Fact]
        public async Task ZeroTotalInvoice_IsRefused_NeverAZeroAmountLink()
        {
            _h.AddSentInvoice(tongTien: 0m);

            var result = await CreateAsync();

            Assert.False(result.Success);
            Assert.Null(result.Data);
        }

        // ---------- Tạo link: thời gian, nội dung, tham số ----------

        [Fact]
        public async Task ExpireDate_RollsOverTheVietnamMidnight()
        {
            _h.AddSentInvoice();
            // 16:50 UTC ngày 03/10 là 23:50 ngày 03/10 giờ Việt Nam; hết hạn sau 15 phút là 00:05 ngày 04/10.
            _h.Time.SetUtcNow(new DateTimeOffset(new DateTime(2026, 10, 3, 16, 50, 0, DateTimeKind.Utc)));

            var query = ParseQuery((await CreateAsync()).Data!);

            Assert.Equal("20261003235000", query["vnp_CreateDate"]);
            Assert.Equal("20261004000500", query["vnp_ExpireDate"]);
        }

        [Fact]
        public async Task ExpireDate_IsAlwaysLaterThanCreateDate_AndTheyAreFourteenDigits()
        {
            _h.AddSentInvoice();

            var query = ParseQuery((await CreateAsync()).Data!);

            Assert.Matches(@"^\d{14}$", query["vnp_CreateDate"]);
            Assert.Matches(@"^\d{14}$", query["vnp_ExpireDate"]);
            Assert.True(string.CompareOrdinal(query["vnp_ExpireDate"], query["vnp_CreateDate"]) > 0);
        }

        [Fact]
        public async Task VietnameseInvoiceCode_GivesAnAsciiOnlyOrderInfo_ThatStillVerifies()
        {
            _h.AddSentInvoice().MaHoaDon = "HĐ-Tháng 10/2026 #3 & đủ?";

            var result = await CreateAsync();

            var query = ParseQuery(result.Data!);
            var info = query["vnp_OrderInfo"];
            Assert.Equal("Thanh toan hoa don HD-Thang 102026 3 du", info);
            Assert.All(info, c => Assert.True(char.IsAsciiLetterOrDigit(c) || c is ' ' or '-' or '_', $"Ký tự không hợp lệ trong vnp_OrderInfo: '{c}'"));
            Assert.True(info.Length <= VnPayProtocol.MaxOrderInfoLength);
            Assert.True(VnPayProtocol.Verify(query, Secret));
        }

        [Fact]
        public async Task TmnCodeWithSurroundingSpaces_IsTrimmedInTheLink()
        {
            _h.AddSentInvoice();

            var query = ParseQuery((await CreateAsync(tmnCode: " TESTTMN1 ")).Data!);

            Assert.Equal(TmnCode, query["vnp_TmnCode"]);
            Assert.True(VnPayProtocol.Verify(query, Secret));
        }

        [Fact]
        public async Task ReturnUrlWithSpecialCharacters_RoundTripsAndStillVerifies()
        {
            _h.AddSentInvoice();
            const string url = "https://abc.ngrok.app/KhachThue/HoaDon/ThanhToan/VnPay/KetQua?a=1&b=hai ba";

            var query = ParseQuery((await CreateAsync(returnUrl: url)).Data!);

            Assert.Equal(url, query["vnp_ReturnUrl"]);
            Assert.True(VnPayProtocol.Verify(query, Secret));
        }

        [Fact]
        public async Task TheLinkContainsNoPersonalDataOfTheTenant()
        {
            _h.AddSentInvoice();

            var url = (await CreateAsync()).Data!;

            Assert.DoesNotContain("0123456789", url);
            Assert.DoesNotContain(Secret + "&", url);
            Assert.Single(System.Text.RegularExpressions.Regex.Matches(url, "vnp_SecureHash="));
        }

        [Fact]
        public async Task DeletedAfterSending_WithTheSendDate_IsRefusedButNotHidden()
        {
            // Quy trình hủy thật: DaHuy + IsDeleted + NgayGui còn giữ (KhachXemDuoc vẫn đúng) → báo lý do, không NotFound.
            var invoice = _h.AddSentInvoice();
            invoice.TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaHuy;
            invoice.IsDeleted = true;

            var result = await CreateAsync();

            Assert.False(result.Success);
            Assert.Equal(ServiceErrorKind.Validation, result.ErrorKind);
            Assert.Contains("không còn ở trạng thái nhận thanh toán", result.Message);
        }

        [Fact]
        public async Task CancelledWithoutEverBeingSent_GetsNotFound()
        {
            var invoice = _h.AddSentInvoice();
            invoice.TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaHuy;
            invoice.NgayGui = null;

            var result = await CreateAsync();

            Assert.Equal(ServiceErrorKind.NotFound, result.ErrorKind);
        }

        // ---------- Trang kết quả: không lộ dữ liệu ----------

        [Theory]
        [InlineData("00")]
        [InlineData("24")]
        [InlineData("51")]
        [InlineData("07")]
        public async Task InvoiceOfAnotherTenant_IsUnverified_ForEveryResponseCode(string code)
        {
            _h.AddSentInvoice();

            AssertUnverified(await ReturnAsync(ReturnQuery(q => q["vnp_ResponseCode"] = code), PaymentTestHarness.TenantId + 1));
        }

        [Fact]
        public async Task TenantIdZeroOrNegative_IsUnverified()
        {
            _h.AddSentInvoice();

            AssertUnverified(await ReturnAsync(ReturnQuery(), nguoiThueId: 0));
            AssertUnverified(await ReturnAsync(ReturnQuery(), nguoiThueId: -1));
        }

        [Theory]
        [InlineData(TrangThaiPhatHanhHoaDon.Nhap)]
        [InlineData(TrangThaiPhatHanhHoaDon.ChoDuyet)]
        [InlineData(TrangThaiPhatHanhHoaDon.DaChot)]
        public async Task InvoiceNotPublishedYet_IsUnverified_EvenForCancelledOrSuccessCodes(TrangThaiPhatHanhHoaDon status)
        {
            _h.AddSentInvoice().TrangThaiPhatHanh = status;

            AssertUnverified(await ReturnAsync(ReturnQuery()));
            AssertUnverified(await ReturnAsync(ReturnQuery(q => q["vnp_ResponseCode"] = "24")));
        }

        [Fact]
        public async Task Success_ForAnInvoiceCancelledAfterSendingAndSoftDeleted_IsKhongGhiNhan()
        {
            // Hóa đơn bị hủy sau khi gửi (DaHuy + IsDeleted + NgayGui): IPN trả 02 nên không bao giờ ghi.
            var invoice = _h.AddSentInvoice();
            invoice.TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaHuy;
            invoice.IsDeleted = true;

            var result = await ReturnAsync(ReturnQuery());

            Assert.Equal(VnPayReturnStatus.KhongGhiNhan, result.TrangThai);
            Assert.Equal(7, result.HoaDonId);
            Assert.Null(result.SoTien);
        }

        [Fact]
        public async Task Success_ForACancelledInvoiceThatWasNeverSent_IsUnverified()
        {
            var invoice = _h.AddSentInvoice();
            invoice.TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaHuy;
            invoice.NgayGui = null;

            AssertUnverified(await ReturnAsync(ReturnQuery()));
        }

        [Fact]
        public async Task UnverifiedResults_LogNoQueryValue_NoSecretAndNoSignature()
        {
            _h.AddSentInvoice();
            var query = ReturnQuery(q => q["vnp_Amount"] = "123400");
            var signature = query["vnp_SecureHash"];
            query["vnp_Amount"] = "200000000";

            await ReturnAsync(query);
            await ReturnAsync(ReturnQuery(q => q["vnp_TmnCode"] = "OTHER"));
            await ReturnAsync(ReturnQuery(), PaymentTestHarness.TenantId + 1);

            Assert.NotEmpty(_log.Entries);
            foreach (var (level, message) in _log.Entries)
            {
                Assert.True(level <= LogLevel.Warning);
                Assert.DoesNotContain(TransactionNo, message);
                Assert.DoesNotContain("200000000", message);
                Assert.DoesNotContain("OTHER", message);
                Assert.DoesNotContain(Secret, message);
                Assert.DoesNotContain(signature, message);
                Assert.DoesNotContain(TxnRef, message);
                Assert.DoesNotContain("Thanh toan hoa don", message);
            }
        }

        // ---------- Trang kết quả: số tiền ở trạng thái đã ghi nhận (X1) ----------

        [Theory]
        [InlineData("abc")]
        [InlineData("150050")]
        [InlineData("0")]
        [InlineData("-200000000")]
        [InlineData("2e8")]
        public async Task Recorded_WithAnUnreadableVnPayAmount_ShowsOnlyTheLedgerAmount(string vnpAmount)
        {
            _h.AddSentInvoice();
            _h.AddVnPayLedger(7, 2_000_000m, TransactionNo, Now);

            var result = await ReturnAsync(ReturnQuery(q => q["vnp_Amount"] = vnpAmount));

            Assert.Equal(VnPayReturnStatus.DaGhiNhan, result.TrangThai);
            Assert.Equal(2_000_000m, result.SoTien);
            Assert.Null(result.SoTienVnPayBao);
        }

        [Fact]
        public async Task Recorded_WhenVnPayReportsLessThanTheLedger_StillShowsBothNumbers()
        {
            _h.AddSentInvoice();
            _h.AddVnPayLedger(7, 2_000_000m, TransactionNo, Now);

            var result = await ReturnAsync(ReturnQuery(q => q["vnp_Amount"] = "100000000"));

            Assert.Equal(VnPayReturnStatus.DaGhiNhan, result.TrangThai);
            Assert.Equal(2_000_000m, result.SoTien);
            Assert.Equal(1_000_000m, result.SoTienVnPayBao);
        }

        [Fact]
        public async Task Recorded_ShowsTheAmountOfTheRowOfThisTransaction_NotOfAnotherRow()
        {
            _h.AddSentInvoice();
            _h.AddVnPayLedger(7, 300_000m, "99999999", Now.AddHours(-1));
            var mine = _h.AddVnPayLedger(7, 1_700_000m, TransactionNo, Now);

            var result = await ReturnAsync(ReturnQuery());

            Assert.Equal(VnPayReturnStatus.DaGhiNhan, result.TrangThai);
            Assert.Equal(mine.LichSuThanhToanId, result.LichSuThanhToanId);
            Assert.Equal(1_700_000m, result.SoTien);
            Assert.Equal(2_000_000m, result.SoTienVnPayBao);
        }

        [Fact]
        public async Task Success_WhenTheSameTransactionNumberWasRecordedOnAnotherInvoice_IsStillDangGhiNhan()
        {
            _h.AddSentInvoice();
            _h.AddSentInvoice(hoaDonId: 8);
            _h.AddVnPayLedger(8, 2_000_000m, TransactionNo, Now);

            var result = await ReturnAsync(ReturnQuery());

            Assert.Equal(VnPayReturnStatus.DangGhiNhan, result.TrangThai);
            Assert.Null(result.LichSuThanhToanId);
            Assert.Null(result.SoTien);
        }

        [Fact]
        public async Task Success_WhenSePayAndVnPayRowsExist_ButNotThisTransaction_IsKhongGhiNhanOnlyWhenTheInvoiceIsPaid()
        {
            _h.AddSentInvoice();
            _h.AddSePayLedger(7, 500_000m, "FT26ABC", Now.AddHours(-1));

            var stillOwing = await ReturnAsync(ReturnQuery());
            _h.AddSePayLedger(7, 1_500_000m, "FT26ABD", Now.AddMinutes(-5));
            var nowPaid = await ReturnAsync(ReturnQuery());

            Assert.Equal(VnPayReturnStatus.DangGhiNhan, stillOwing.TrangThai);
            Assert.Equal(VnPayReturnStatus.KhongGhiNhan, nowPaid.TrangThai);
        }

        [Fact]
        public async Task ReturnPage_NeverWritesAnything_InAnyOfTheSixStates()
        {
            _h.AddSentInvoice();
            await ReturnAsync(ReturnQuery(sign: false));
            await ReturnAsync(ReturnQuery(q => q["vnp_ResponseCode"] = "24"));
            await ReturnAsync(ReturnQuery(q => q["vnp_ResponseCode"] = "07"));
            await ReturnAsync(ReturnQuery());
            _h.AddSePayLedger(7, 2_000_000m, "FT26ABC", Now);
            await ReturnAsync(ReturnQuery());
            _h.AddVnPayLedger(7, 100m, TransactionNo, Now);
            var ledgerCountBefore = _h.Store.Ledger.Count;
            await ReturnAsync(ReturnQuery());

            Assert.Equal(ledgerCountBefore, _h.Store.Ledger.Count);
            Assert.Equal(0, _h.Store.LockCalls);
            Assert.Empty(_h.UnitOfWork.Transactions);
            Assert.Equal(0, _h.UnitOfWork.SaveChangesCalls);
            Assert.Empty(_h.Store.Notifications);
            Assert.Empty(_h.Notifier.PaymentUpdates);
        }
    }
}
