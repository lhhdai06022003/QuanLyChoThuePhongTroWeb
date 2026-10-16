using QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    public class TransferCodeParserTests
    {
        [Theory]
        [InlineData("TTPTHD105", "TTPTHD105")]
        [InlineData("ttpthd105", "TTPTHD105")]
        [InlineData(" TTPT HD 105 ", "TTPTHD105")]
        public void Extract_CodeField_IsNormalized(string code, string expected)
        {
            Assert.Equal(expected, TransferCodeParser.Extract(code, null, null));
        }

        [Fact]
        public void Extract_CodeField_WinsOverContentAndDescription()
        {
            var result = TransferCodeParser.Extract("TTPTHD105", "TTPTHD999", "TTPTHD998");

            Assert.Equal("TTPTHD105", result);
        }

        [Fact]
        public void Extract_CodeFieldWithOtherPrefix_IsReturnedAsIs_WithoutFallingBackToContent()
        {
            var result = TransferCodeParser.Extract("dh 42", "TTPTHD105", null);

            Assert.Equal("DH42", result);
        }

        [Theory]
        [InlineData("TTPTHD105", "TTPTHD105")]
        [InlineData("ttpt hd 105", "TTPTHD105")]
        [InlineData("TTPT HD105-FT26ABCDE", "TTPTHD105")]
        [InlineData("NGUYEN VAN A chuyen TTPTHD105 tien phong thang 9", "TTPTHD105")]
        [InlineData("TTPTHD 105", "TTPTHD105")]
        [InlineData("TTPTHD00105", "TTPTHD00105")]
        public void Extract_CodeEmpty_ReadsContent(string content, string expected)
        {
            Assert.Equal(expected, TransferCodeParser.Extract(null, content, null));
            Assert.Equal(expected, TransferCodeParser.Extract(string.Empty, content, null));
            Assert.Equal(expected, TransferCodeParser.Extract("   ", content, null));
        }

        [Fact]
        public void Extract_OnlyInDescription_ReadsDescription()
        {
            var result = TransferCodeParser.Extract(null, "chuyen tien tro", "BANK TTPTHD42 FT26");

            Assert.Equal("TTPTHD42", result);
        }

        [Fact]
        public void Extract_ContentWinsOverDescription()
        {
            var result = TransferCodeParser.Extract(null, "TTPTHD7", "TTPTHD8");

            Assert.Equal("TTPTHD7", result);
        }

        [Fact]
        public void Extract_TwoCodes_TakesTheFirst()
        {
            var result = TransferCodeParser.Extract(null, "TTPTHD105 va TTPTHD106", null);

            Assert.Equal("TTPTHD105", result);
        }

        [Theory]
        [InlineData(null, null, null)]
        [InlineData("", "", "")]
        [InlineData(null, "chuyen tien tro thang 9", "thanh toan")]
        [InlineData(null, "TTPTHD", null)]
        [InlineData(null, "TTPT 105", null)]
        public void Extract_NoCode_ReturnsNull(string? code, string? content, string? description)
        {
            Assert.Null(TransferCodeParser.Extract(code, content, description));
        }

        [Fact]
        public void Extract_MoreThanNineDigits_IsNotAMatch()
        {
            Assert.Null(TransferCodeParser.Extract(null, "TTPTHD1234567890", null));
            Assert.Equal("TTPTHD123456789", TransferCodeParser.Extract(null, "TTPTHD123456789", null));
        }

        [Fact]
        public void Extract_NonAsciiDigits_AreNotAMatch()
        {
            // Chữ số Ả Rập-Ấn Độ: \d của .NET khớp nhưng int.Parse thì không, nên mẫu chỉ nhận 0-9.
            Assert.Null(TransferCodeParser.Extract(null, "TTPTHD١٠٥", null));
        }
    }
}
