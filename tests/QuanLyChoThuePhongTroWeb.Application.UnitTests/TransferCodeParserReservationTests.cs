using QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    // Mã giữ chỗ TTPTGC{id} cạnh mã hóa đơn TTPTHD{id} (spec giữ chỗ §6.1, X8).
    public class TransferCodeParserReservationTests
    {
        [Theory]
        [InlineData("TTPTGC12", "TTPTGC12")]
        [InlineData("ttpt gc 12", "TTPTGC12")]
        [InlineData("NGUYEN VAN A TTPTGC57 giu cho phong", "TTPTGC57")]
        [InlineData("TTPTGC007", "TTPTGC007")]
        public void Extract_ReadsTheReservationCodeFromContent(string content, string expected)
        {
            Assert.Equal(expected, TransferCodeParser.Extract(null, content, null));
        }

        [Fact]
        public void Extract_CodeField_IsNormalizedAsIs()
        {
            Assert.Equal("TTPTGC12", TransferCodeParser.Extract("ttptgc12", "TTPTHD5", null));
        }

        [Theory]
        [InlineData("TTPTGC1234567890")]
        [InlineData("GC1A2B3C4D5E6F")]
        [InlineData("TTPTGC")]
        public void Extract_InvalidReservationCode_ReturnsNull(string content)
        {
            Assert.Null(TransferCodeParser.Extract(null, content, null));
        }

        [Fact]
        public void Extract_OnlyInvoiceCode_IsUnchanged()
        {
            Assert.Equal("TTPTHD5", TransferCodeParser.Extract(null, "TTPTHD5", null));
        }

        [Theory]
        [InlineData("TTPTHD5 va TTPTGC7", "TTPTHD5")]
        [InlineData("TTPTGC7 va TTPTHD5", "TTPTGC7")]
        public void Extract_BothCodes_TakesTheOneThatAppearsFirst(string content, string expected)
        {
            Assert.Equal(expected, TransferCodeParser.Extract(null, content, null));
        }

        [Fact]
        public void Extract_ContentWithoutCode_FallsBackToDescription()
        {
            Assert.Equal("TTPTGC9", TransferCodeParser.Extract(null, "chuyen tien", "BANK TTPTGC9 FT26"));
        }

        [Fact]
        public void ExtractReservationId_ReadsOnlyReservationCodes()
        {
            Assert.Equal(57, TransferCodeParser.ExtractReservationId("TTPTGC57"));
            Assert.Equal(7, TransferCodeParser.ExtractReservationId("TTPTGC007"));
            Assert.Null(TransferCodeParser.ExtractReservationId("TTPTHD12"));
            Assert.Null(TransferCodeParser.ExtractReservationId("TTPTGC0"));
            Assert.Null(TransferCodeParser.ExtractReservationId("ttptgc12"));
            Assert.Null(TransferCodeParser.ExtractReservationId("TTPTGC1234567890"));
            Assert.Null(TransferCodeParser.ExtractReservationId(null));
            Assert.Null(TransferCodeParser.ExtractReservationId(string.Empty));
        }

        [Fact]
        public void ExtractInvoiceId_IgnoresReservationCodes()
        {
            Assert.Null(TransferCodeParser.ExtractInvoiceId("TTPTGC12"));
        }
    }
}
