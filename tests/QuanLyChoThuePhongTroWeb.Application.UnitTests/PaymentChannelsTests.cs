using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Payments;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    public class PaymentChannelsTests
    {
        [Theory]
        [InlineData("VNPAY-123", PhuongThucThanhToan.ChuyenKhoan, KenhThu.VnPay)]
        [InlineData("SEPAY-ABC", PhuongThucThanhToan.ChuyenKhoan, KenhThu.SePay)]
        [InlineData("sepay-x", PhuongThucThanhToan.ChuyenKhoan, KenhThu.SePay)]
        [InlineData("SEPAY-ID-9", PhuongThucThanhToan.ChuyenKhoan, KenhThu.SePay)]
        [InlineData("CANTRU-HD1-2", PhuongThucThanhToan.CanTruTienCoc, KenhThu.CanTruCoc)]
        [InlineData("TM-1", PhuongThucThanhToan.TienMat, KenhThu.TienMat)]
        [InlineData("CK-1", PhuongThucThanhToan.ChuyenKhoan, KenhThu.ChuyenKhoan)]
        [InlineData("", PhuongThucThanhToan.ChuyenKhoan, KenhThu.ChuyenKhoan)]
        public void Of_PhanLoaiDungKenh(string ma, PhuongThucThanhToan phuongThuc, KenhThu expected)
        {
            Assert.Equal(expected, PaymentChannels.Of(ma, phuongThuc));
        }

        [Fact]
        public void Of_MaNull_KhongNem()
        {
            Assert.Equal(KenhThu.TienMat, PaymentChannels.Of(null, PhuongThucThanhToan.TienMat));
        }
    }
}
