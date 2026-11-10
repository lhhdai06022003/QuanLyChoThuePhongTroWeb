using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Payments;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    public class KenhThuSlugsTests
    {
        [Theory]
        [InlineData("tien-mat", KenhThu.TienMat)]
        [InlineData("chuyen-khoan", KenhThu.ChuyenKhoan)]
        [InlineData("sepay", KenhThu.SePay)]
        [InlineData("vnpay", KenhThu.VnPay)]
        [InlineData("can-tru-coc", KenhThu.CanTruCoc)]
        [InlineData("SePay", KenhThu.SePay)]
        [InlineData("  VNPAY  ", KenhThu.VnPay)]
        [InlineData("Tien-Mat", KenhThu.TienMat)]
        public void TryParse_NhanDungSlug_KhongPhanBietHoaThuongVaKhoangTrang(string slug, KenhThu expected)
        {
            Assert.True(KenhThuSlugs.TryParse(slug, out var kenh));
            Assert.Equal(expected, kenh);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("xyz")]
        [InlineData("0")]
        [InlineData("-1")]
        [InlineData("tien mat")]
        public void TryParse_RongHoacSai_TraFalse(string? slug)
        {
            Assert.False(KenhThuSlugs.TryParse(slug, out _));
        }

        [Theory]
        [InlineData(KenhThu.TienMat, "tien-mat")]
        [InlineData(KenhThu.ChuyenKhoan, "chuyen-khoan")]
        [InlineData(KenhThu.SePay, "sepay")]
        [InlineData(KenhThu.VnPay, "vnpay")]
        [InlineData(KenhThu.CanTruCoc, "can-tru-coc")]
        public void Slug_VaTryParse_LaHaiChieuNguocNhau(KenhThu kenh, string slug)
        {
            Assert.Equal(slug, KenhThuSlugs.Slug(kenh));
            Assert.True(KenhThuSlugs.TryParse(slug, out var back));
            Assert.Equal(kenh, back);
        }
    }
}
