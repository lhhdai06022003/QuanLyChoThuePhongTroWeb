using QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    // Tên file ảnh QR khách tải về (spec thanh toán trực tuyến §3.2 mục 2): chỉ gồm ASCII [A-Za-z0-9_-].
    public class PaymentQrFileNameTests
    {
        [Theory]
        [InlineData("P101", "QR_Phong_P101_HD5.png")]
        [InlineData("Phòng Đ-1/ạ", "QR_Phong_Phong_D-1a_HD5.png")]
        [InlineData("đường Ơ", "QR_Phong_duong_O_HD5.png")]
        [InlineData("A  B", "QR_Phong_A__B_HD5.png")]
        [InlineData("../x\\y", "QR_Phong_xy_HD5.png")]
        [InlineData("!!!", "QR_Phong_HD5.png")]
        [InlineData("", "QR_Phong_HD5.png")]
        public void Build_KeepsOnlyAsciiLettersDigitsUnderscoreAndHyphen(string tenPhong, string expected)
        {
            Assert.Equal(expected, PaymentQrFileName.Build(tenPhong, 5));
        }

        [Fact]
        public void Build_NullRoomName_FallsBackToTheShortName()
        {
            Assert.Equal("QR_Phong_HD12.png", PaymentQrFileName.Build(null!, 12));
        }
    }
}
