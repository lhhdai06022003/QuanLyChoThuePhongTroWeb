using QuanLyChoThuePhongTroWeb.Application.Common.Configurations;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    public class OnlinePaymentOptionsTests
    {
        [Fact]
        public void Defaults_AreValid_AndHaveNoPublicUrl()
        {
            var options = new OnlinePaymentOptions();

            options.Validate();

            Assert.Equal(string.Empty, options.PublicBaseUrl);
            Assert.Null(options.BuildUrl("/KhachThue/HoaDon?hoaDonId=7"));
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void BlankPublicBaseUrl_IsValid(string url)
        {
            new OnlinePaymentOptions { PublicBaseUrl = url }.Validate();
        }

        [Theory]
        [InlineData("https://tro.ngrok-free.app")]
        [InlineData("http://localhost:5009")]
        [InlineData("https://tro.test/")]
        [InlineData("  https://tro.test  ")]
        public void AbsoluteHttpOrHttpsUrl_IsValid(string url)
        {
            new OnlinePaymentOptions { PublicBaseUrl = url }.Validate();
        }

        [Theory]
        [InlineData("tro.test")]
        [InlineData("/KhachThue")]
        [InlineData("ftp://tro.test")]
        [InlineData("javascript:alert(1)")]
        [InlineData("mailto:a@b.vn")]
        [InlineData("not a url")]
        public void NotAnAbsoluteHttpUrl_Throws(string url)
        {
            var ex = Assert.Throws<InvalidOperationException>(() => new OnlinePaymentOptions { PublicBaseUrl = url }.Validate());

            Assert.Equal("Cấu hình OnlinePayment:PublicBaseUrl phải là URL tuyệt đối http hoặc https.", ex.Message);
        }

        [Theory]
        [InlineData("https://tro.test", "https://tro.test/KhachThue/HoaDon?hoaDonId=7")]
        [InlineData("https://tro.test/", "https://tro.test/KhachThue/HoaDon?hoaDonId=7")]
        [InlineData("https://tro.test///", "https://tro.test/KhachThue/HoaDon?hoaDonId=7")]
        [InlineData("  https://tro.test/  ", "https://tro.test/KhachThue/HoaDon?hoaDonId=7")]
        [InlineData("https://tro.test/app", "https://tro.test/app/KhachThue/HoaDon?hoaDonId=7")]
        public void BuildUrl_JoinsTheBaseUrlAndThePath_WithoutADoubleSlash(string baseUrl, string expected)
        {
            var options = new OnlinePaymentOptions { PublicBaseUrl = baseUrl };

            Assert.Equal(expected, options.BuildUrl("/KhachThue/HoaDon?hoaDonId=7"));
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void BuildUrl_WithoutABaseUrl_IsNull(string baseUrl)
        {
            Assert.Null(new OnlinePaymentOptions { PublicBaseUrl = baseUrl }.BuildUrl("/KhachThue/HoaDon?hoaDonId=7"));
        }
    }
}
