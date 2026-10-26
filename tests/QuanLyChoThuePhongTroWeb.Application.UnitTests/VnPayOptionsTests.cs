using System;
using QuanLyChoThuePhongTroWeb.Application.Common.Configurations;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    // Cấu hình VnPay (spec VNPay §10): mặc định tắt, bật thì bắt buộc TmnCode và HashSecret.
    public class VnPayOptionsTests
    {
        [Fact]
        public void Defaults_AreValid_AndVnPayIsDisabled()
        {
            var options = new VnPayOptions();

            options.Validate();

            Assert.False(options.Enabled);
            Assert.Equal(15, options.ExpireMinutes);
            Assert.Equal("https://sandbox.vnpayment.vn/paymentv2/vpcpay.html", options.PaymentUrl);
        }

        [Fact]
        public void Disabled_WithoutTmnCodeOrSecret_IsValid()
        {
            new VnPayOptions { Enabled = false, TmnCode = "", HashSecret = "" }.Validate();
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void Enabled_WithoutTmnCode_Throws(string tmnCode)
        {
            var ex = Assert.Throws<InvalidOperationException>(() =>
                new VnPayOptions { Enabled = true, TmnCode = tmnCode, HashSecret = "secret" }.Validate());

            Assert.Contains("VnPay:TmnCode", ex.Message);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void Enabled_WithoutHashSecret_Throws(string secret)
        {
            var ex = Assert.Throws<InvalidOperationException>(() =>
                new VnPayOptions { Enabled = true, TmnCode = "TESTTMN1", HashSecret = secret }.Validate());

            Assert.Contains("VnPay:HashSecret", ex.Message);
        }

        [Fact]
        public void Enabled_WithTmnCodeAndSecret_IsValid()
        {
            new VnPayOptions { Enabled = true, TmnCode = "TESTTMN1", HashSecret = "secret" }.Validate();
        }

        [Theory]
        [InlineData("http://sandbox.vnpayment.vn/paymentv2/vpcpay.html")]
        [InlineData("/paymentv2/vpcpay.html")]
        [InlineData("")]
        [InlineData("not a url")]
        public void PaymentUrl_NotAbsoluteHttps_Throws(string url)
        {
            var ex = Assert.Throws<InvalidOperationException>(() => new VnPayOptions { PaymentUrl = url }.Validate());

            Assert.Contains("VnPay:PaymentUrl", ex.Message);
        }

        // BuildPaymentUrl luôn nối "?" sau PaymentUrl: URL có sẵn query hoặc fragment bị chặn lúc khởi động.
        [Theory]
        [InlineData("https://sandbox.vnpayment.vn/paymentv2/vpcpay.html?lang=vn")]
        [InlineData("https://sandbox.vnpayment.vn/paymentv2/vpcpay.html?")]
        [InlineData("https://sandbox.vnpayment.vn/paymentv2/vpcpay.html#top")]
        public void PaymentUrl_WithAQueryOrFragment_Throws(string url)
        {
            var ex = Assert.Throws<InvalidOperationException>(() => new VnPayOptions { PaymentUrl = url }.Validate());

            Assert.Equal("Cấu hình VnPay:PaymentUrl không được chứa '?' hoặc '#'.", ex.Message);
        }

        [Theory]
        [InlineData(4)]
        [InlineData(61)]
        [InlineData(0)]
        [InlineData(-1)]
        public void ExpireMinutes_OutOfRange_Throws(int minutes)
        {
            var ex = Assert.Throws<InvalidOperationException>(() => new VnPayOptions { ExpireMinutes = minutes }.Validate());

            Assert.Contains("VnPay:ExpireMinutes", ex.Message);
        }

        [Theory]
        [InlineData(5)]
        [InlineData(60)]
        public void ExpireMinutes_AtTheBoundaries_IsValid(int minutes)
        {
            new VnPayOptions { ExpireMinutes = minutes }.Validate();
        }
    }
}
