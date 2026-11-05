using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Payments;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    public class PaymentMethodLabelsTests
    {
        [Theory]
        [InlineData(PhuongThucThanhToan.TienMat, "Tiền mặt")]
        [InlineData(PhuongThucThanhToan.ChuyenKhoan, "Chuyển khoản")]
        [InlineData(PhuongThucThanhToan.CanTruTienCoc, "Cấn trừ tiền cọc")]
        public void Label_ReturnsVietnameseName(PhuongThucThanhToan phuongThuc, string expected)
        {
            Assert.Equal(expected, PaymentMethodLabels.Label(phuongThuc));
        }

        [Fact]
        public void Label_UnknownValue_FallsBackToTransfer()
        {
            Assert.Equal("Chuyển khoản", PaymentMethodLabels.Label((PhuongThucThanhToan)99));
        }
    }
}
