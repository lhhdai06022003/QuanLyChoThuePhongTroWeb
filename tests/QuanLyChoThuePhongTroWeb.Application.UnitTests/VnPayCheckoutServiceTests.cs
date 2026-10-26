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
    // Tạo link thanh toán VNPay (spec VNPay §4.2, §5.1): kiểm lúc bấm, số tiền = số còn lại × 100, không ghi DB.
    public class VnPayCheckoutServiceTests
    {
        private const string Secret = "secret";
        private const string TmnCode = "TESTTMN1";
        private const string ReturnUrl = "http://localhost:5009/KhachThue/HoaDon/ThanhToan/VnPay/KetQua";
        private const string ClientIp = "203.0.113.9";
        private static readonly DateTime Now = PaymentTestHarness.Now;

        private readonly PaymentTestHarness _h = new();
        private readonly CapturingLogger<VnPayPaymentService> _log = new();

        private VnPayPaymentService Create(int expireMinutes = 15)
        {
            var options = new VnPayOptions { Enabled = true, TmnCode = TmnCode, HashSecret = Secret, ExpireMinutes = expireMinutes };
            var publisher = new PaymentOutcomePublisher(_h.Notifier, new RecordingPaymentNotificationQueue(), new FakeTransferAlertDeduplicator(), NullLogger<PaymentOutcomePublisher>.Instance);
            return new VnPayPaymentService(options, _h.Store, new InvoiceTransferHandler(_h.Transaction), publisher, _h.Time, _log);
        }

        private Task<ServiceResult<string>> CreateAsync(int hoaDonId = 7, int nguoiThueId = PaymentTestHarness.TenantId, int expireMinutes = 15)
            => Create(expireMinutes).CreatePaymentUrlAsync(hoaDonId, nguoiThueId, ClientIp, ReturnUrl);

        private static Dictionary<string, string> ParseQuery(string url)
        {
            return url[(url.IndexOf('?') + 1)..].Split('&')
                .Select(pair => pair.Split('=', 2))
                .ToDictionary(p => p[0], p => WebUtility.UrlDecode(p[1]), StringComparer.Ordinal);
        }

        private void AssertNothingWritten()
        {
            Assert.Equal(0, _h.Store.LockCalls);
            Assert.Empty(_h.UnitOfWork.Transactions);
            Assert.Equal(0, _h.UnitOfWork.SaveChangesCalls);
            Assert.Empty(_h.Store.Notifications);
        }

        [Fact]
        public async Task SentInvoice_BuildsASignedUrl_WithAllTheParametersOfSpec51()
        {
            _h.AddSentInvoice();

            var result = await CreateAsync();

            Assert.True(result.Success);
            var url = result.Data!;
            Assert.StartsWith("https://sandbox.vnpayment.vn/paymentv2/vpcpay.html?", url);
            var query = ParseQuery(url);
            Assert.Equal("2.1.0", query["vnp_Version"]);
            Assert.Equal("pay", query["vnp_Command"]);
            Assert.Equal(TmnCode, query["vnp_TmnCode"]);
            Assert.Equal("200000000", query["vnp_Amount"]);
            Assert.Equal("VND", query["vnp_CurrCode"]);
            Assert.Equal(VnPayProtocol.BuildTxnRef(7, Now), query["vnp_TxnRef"]);
            Assert.Equal("Thanh toan hoa don HD-7", query["vnp_OrderInfo"]);
            Assert.Equal("other", query["vnp_OrderType"]);
            Assert.Equal("vn", query["vnp_Locale"]);
            Assert.Equal(ReturnUrl, query["vnp_ReturnUrl"]);
            Assert.Equal(ClientIp, query["vnp_IpAddr"]);
            Assert.False(query.ContainsKey("vnp_BankCode"));
            Assert.True(VnPayProtocol.Verify(query, Secret));
        }

        [Fact]
        public async Task CreateAndExpireDates_AreVietnamTime_AndFollowExpireMinutes()
        {
            _h.AddSentInvoice();

            var defaultQuery = ParseQuery((await CreateAsync()).Data!);
            var longQuery = ParseQuery((await CreateAsync(expireMinutes: 30)).Data!);

            // Now = 03:00 UTC = 10:00 giờ Việt Nam.
            Assert.Equal("20261003100000", defaultQuery["vnp_CreateDate"]);
            Assert.Equal("20261003101500", defaultQuery["vnp_ExpireDate"]);
            Assert.Equal("20261003103000", longQuery["vnp_ExpireDate"]);
        }

        [Fact]
        public async Task AfterAPartialPayment_TheAmountIsTheRemainingBalance()
        {
            _h.AddSentInvoice();
            _h.AddLedger(7, 500_000m);

            var result = await CreateAsync();

            Assert.Equal("150000000", ParseQuery(result.Data!)["vnp_Amount"]);
        }

        [Fact]
        public async Task EveryCall_UsesAnotherOrderReference_WhenTheTimeMoves()
        {
            _h.AddSentInvoice();
            var first = ParseQuery((await CreateAsync()).Data!)["vnp_TxnRef"];
            _h.Time.SetUtcNow(new DateTimeOffset(Now.AddSeconds(5)));

            var second = ParseQuery((await CreateAsync()).Data!)["vnp_TxnRef"];

            Assert.NotEqual(first, second);
            Assert.True(VnPayProtocol.TryParseTxnRef(second, out var hoaDonId));
            Assert.Equal(7, hoaDonId);
        }

        [Fact]
        public async Task AnotherTenant_GetsNotFound()
        {
            _h.AddSentInvoice();

            var result = await CreateAsync(nguoiThueId: PaymentTestHarness.TenantId + 1);

            Assert.False(result.Success);
            Assert.Equal(ServiceErrorKind.NotFound, result.ErrorKind);
            Assert.Equal("Không tìm thấy hóa đơn.", result.Message);
        }

        [Fact]
        public async Task MissingInvoice_GetsNotFound()
        {
            var result = await CreateAsync(hoaDonId: 999);

            Assert.Equal(ServiceErrorKind.NotFound, result.ErrorKind);
        }

        [Theory]
        [InlineData(TrangThaiPhatHanhHoaDon.Nhap)]
        [InlineData(TrangThaiPhatHanhHoaDon.ChoDuyet)]
        [InlineData(TrangThaiPhatHanhHoaDon.DaChot)]
        public async Task InvoiceNotPublishedYet_GetsNotFound(TrangThaiPhatHanhHoaDon status)
        {
            _h.AddSentInvoice().TrangThaiPhatHanh = status;

            var result = await CreateAsync();

            Assert.Equal(ServiceErrorKind.NotFound, result.ErrorKind);
        }

        [Fact]
        public async Task InvoiceCancelledAfterSending_IsRefused()
        {
            _h.AddSentInvoice().TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaHuy;

            var result = await CreateAsync();

            Assert.False(result.Success);
            Assert.Equal(ServiceErrorKind.Validation, result.ErrorKind);
            Assert.Contains("không còn ở trạng thái nhận thanh toán", result.Message);
        }

        [Fact]
        public async Task SoftDeletedInvoice_GetsNotFound()
        {
            // Hóa đơn đã xóa mềm không còn trong luồng thanh toán của khách (InvoicePaymentTarget.KhachXemDuoc).
            _h.AddSentInvoice().IsDeleted = true;

            var result = await CreateAsync();

            Assert.False(result.Success);
            Assert.Equal(ServiceErrorKind.NotFound, result.ErrorKind);
        }

        [Fact]
        public async Task FullyPaidInvoice_IsRefused()
        {
            _h.AddSentInvoice();
            _h.AddLedger(7, 2_000_000m);

            var result = await CreateAsync();

            Assert.False(result.Success);
            Assert.Contains("đã được thanh toán đủ", result.Message);
        }

        [Fact]
        public async Task FractionalTotal_IsRefused_WithTheFractionalMessage()
        {
            _h.AddSentInvoice(tongTien: 2_000_000.5m);

            var result = await CreateAsync();

            Assert.False(result.Success);
            Assert.Equal("Hóa đơn có số tiền lẻ, vui lòng liên hệ quản lý để lập lại hóa đơn.", result.Message);
        }

        [Fact]
        public async Task FractionalRemainder_IsRefused_EvenWhenTheTotalIsWhole()
        {
            _h.AddSentInvoice();
            _h.AddLedger(7, 500_000.5m);

            var result = await CreateAsync();

            Assert.False(result.Success);
            Assert.Equal("Hóa đơn có số tiền lẻ, vui lòng liên hệ quản lý để lập lại hóa đơn.", result.Message);
        }

        [Fact]
        public async Task CreatingALink_NeverWritesOrLocks_AndNeverLogsTheUrlOrTheSecret()
        {
            _h.AddSentInvoice();

            var result = await CreateAsync();

            Assert.True(result.Success);
            AssertNothingWritten();
            Assert.Empty(_h.Store.Ledger);
            var entry = Assert.Single(_log.Entries);
            Assert.Equal(LogLevel.Information, entry.Level);
            Assert.DoesNotContain(Secret, entry.Message);
            Assert.DoesNotContain("vnp_SecureHash", entry.Message);
            Assert.DoesNotContain("vpcpay", entry.Message);
            Assert.DoesNotContain("Thanh toan hoa don", entry.Message);
        }

        [Fact]
        public async Task RefusedRequests_NeverWriteEither()
        {
            _h.AddSentInvoice();
            _h.AddLedger(7, 2_000_000m);

            await CreateAsync();
            await CreateAsync(nguoiThueId: 999);

            AssertNothingWritten();
        }
    }
}
