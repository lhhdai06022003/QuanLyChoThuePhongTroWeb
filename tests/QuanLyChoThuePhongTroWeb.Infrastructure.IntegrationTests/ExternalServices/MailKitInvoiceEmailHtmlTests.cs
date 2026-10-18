using System;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.DTOs;
using QuanLyChoThuePhongTroWeb.Infrastructure.ExternalServices.Emails;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.ExternalServices
{
    // Hàm thuần, không cần DB: nút thanh toán trực tuyến, dòng nội dung chuyển khoản và khối QR trong email hóa đơn
    // (spec thanh toán trực tuyến §7.1).
    public class MailKitInvoiceEmailHtmlTests
    {
        private const string PaymentUrl = "https://tro.test/KhachThue/HoaDon?hoaDonId=7";

        private static HoaDonChiTietRes Invoice(decimal tong = 2_000_000m, decimal conLai = 1_500_000m, bool daHuy = false) => new()
        {
            HoaDonId = 7,
            MaHoaDon = "HD-202609-0007",
            TongTien = tong,
            DaThu = tong - conLai,
            ConLai = conLai,
            DaHuy = daHuy
        };

        // ---------- Nút thanh toán và dòng nội dung ----------

        [Fact]
        public void WithALink_HasTheLargeButton_TheEncodedHref_AndTheTransferCode()
        {
            var html = MailKitEmailService.BuildOnlinePaymentHtml(Invoice(), PaymentUrl);

            Assert.Contains("THANH TOÁN TRỰC TUYẾN", html);
            Assert.Contains($"<a href='{PaymentUrl}'", html);
            Assert.Contains("font-size:16px", html);
            Assert.Contains("Nội dung chuyển khoản: <code", html);
            Assert.Contains(">TTPTHD7</code>", html);
        }

        [Fact]
        public void TheLinkIsHtmlEncoded_SoItCannotBreakOutOfTheAttribute()
        {
            var html = MailKitEmailService.BuildOnlinePaymentHtml(Invoice(), "https://tro.test/a?b=1&c='2'><script>");

            Assert.Contains("href='https://tro.test/a?b=1&amp;c=&#39;2&#39;&gt;&lt;script&gt;'", html);
            Assert.DoesNotContain("<script>", html);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void WithoutALink_HasNoButton_ButStillShowsTheTransferCode(string? paymentUrl)
        {
            var html = MailKitEmailService.BuildOnlinePaymentHtml(Invoice(), paymentUrl);

            Assert.DoesNotContain("THANH TOÁN TRỰC TUYẾN", html);
            Assert.DoesNotContain("<a ", html);
            Assert.Contains("Nội dung chuyển khoản", html);
            Assert.Contains(">TTPTHD7</code>", html);
        }

        [Fact]
        public void PartiallyPaidInvoice_StillHasTheButton()
        {
            var html = MailKitEmailService.BuildOnlinePaymentHtml(Invoice(conLai: 100_000m), PaymentUrl);

            Assert.Contains("THANH TOÁN TRỰC TUYẾN", html);
        }

        [Theory]
        [InlineData(0, false)]
        [InlineData(-1, false)]
        [InlineData(1_500_000, true)]
        public void CancelledOrFullyPaidInvoice_HasNeitherTheButtonNorTheTransferCode(decimal conLai, bool daHuy)
        {
            Assert.Equal(string.Empty, MailKitEmailService.BuildOnlinePaymentHtml(Invoice(conLai: conLai, daHuy: daHuy), PaymentUrl));
        }

        // ---------- Khối QR ----------

        private static string QrSection(HoaDonChiTietRes invoice, string bankId = "MB", string accountNumber = "0123456789", string accountName = "NHA TRO")
            => MailKitEmailService.BuildInvoiceQrSectionHtml(invoice, bankId, accountNumber, accountName);

        [Fact]
        public void Qr_CarriesTheTransferCodeAndTheRemainingAmount()
        {
            var html = QrSection(Invoice());

            Assert.Contains("addInfo=TTPTHD7", html);
            Assert.Contains("amount=1500000", html);
            Assert.Contains("https://img.vietqr.io/image/MB-0123456789-compact2.png?", html);
            Assert.Contains(">TTPTHD7</code>", html);
            Assert.Contains($"{string.Format("{0:#,##0}", 1_500_000m)} đ", html);
        }

        [Fact]
        public void Qr_NoLongerUsesTheInvoiceCodeTextOrTheTotal()
        {
            var html = QrSection(Invoice());

            Assert.DoesNotContain("THANH TOAN", html);
            Assert.DoesNotContain("THANH%20TOAN", html);
            Assert.DoesNotContain("HD-202609-0007", html);
            Assert.DoesNotContain("amount=2000000", html);
        }

        [Fact]
        public void Qr_WholeAmountIsWrittenWithoutDecimals_RegardlessOfTheServerCulture()
        {
            var html = QrSection(Invoice(tong: 2_000_000m, conLai: 1_500_000.00m));

            Assert.Contains("amount=1500000&amp;addInfo", html);
            Assert.DoesNotContain("amount=1500000.00", html);
        }

        [Theory]
        [InlineData(0, false)]
        [InlineData(-1, false)]
        [InlineData(1_500_000, true)]
        public void Qr_IsEmpty_ForACancelledOrFullyPaidInvoice(decimal conLai, bool daHuy)
        {
            Assert.Equal(string.Empty, QrSection(Invoice(conLai: conLai, daHuy: daHuy)));
        }

        [Fact]
        public void Qr_EncodesBankDetailsFromConfiguration()
        {
            var html = QrSection(Invoice(), bankId: "MB", accountNumber: "0123456789", accountName: "NHA <TRO> & CO");

            Assert.DoesNotContain("<TRO>", html);
            Assert.Contains("NHA &lt;TRO&gt; &amp; CO", html);
            Assert.Contains("accountName=NHA%20%3CTRO%3E%20%26%20CO", html);
        }

        [Fact]
        public void Qr_KeepsTheBankSectionClassSoTheEmailStylesStillApply()
        {
            var html = QrSection(Invoice());

            Assert.Contains("<div class='qr-section'>", html);
            Assert.Contains("Quét mã QR để chuyển khoản nhanh", html);
        }
    }
}
