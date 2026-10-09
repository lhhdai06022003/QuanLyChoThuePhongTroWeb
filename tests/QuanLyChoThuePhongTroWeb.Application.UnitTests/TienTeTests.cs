using QuanLyChoThuePhongTroWeb.Application.Common.Formatting;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    // Định dạng tiền "3.590.000 đ" độc lập culture máy chủ.
    public class TienTeTests
    {
        [Theory]
        [InlineData(3590000, "3.590.000 đ")]
        [InlineData(0, "0 đ")]
        [InlineData(999, "999 đ")]
        [InlineData(1234567890, "1.234.567.890 đ")]
        [InlineData(-1500000, "-1.500.000 đ")]
        public void DinhDang_FormatsWithDotSeparatorAndSuffix(long soTien, string expected)
        {
            Assert.Equal(expected, TienTe.DinhDang(soTien));
        }

        [Fact]
        public void DinhDang_RoundsHalfAwayFromZero()
        {
            Assert.Equal("1.501 đ", TienTe.DinhDang(1500.5m));
        }
    }
}
