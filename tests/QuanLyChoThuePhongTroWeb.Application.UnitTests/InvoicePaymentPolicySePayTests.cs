using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Payments;
using QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    // Nhận diện dòng sổ do SePay ghi và mã tham chiếu ngân hàng (spec thanh toán trực tuyến §5.2, §7.2), dùng chung cho biên lai và chi tiết hóa đơn.
    public class InvoicePaymentPolicySePayTests
    {
        [Theory]
        [InlineData("SEPAY-FT1", true)]
        [InlineData("sepay-ft1", true)]
        [InlineData("SEPAY-ID-5", true)]
        [InlineData("SEPAY-", true)]
        [InlineData("GD-1", false)]
        [InlineData("TM-SEPAY-1", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void IsSePayKey_RecognizesThePrefix_IgnoringCase(string? maGiaoDich, bool expected)
        {
            Assert.Equal(expected, InvoicePaymentPolicy.IsSePayKey(maGiaoDich));
        }

        [Theory]
        [InlineData("SEPAY-FT1", "FT1")]
        [InlineData("sepay-ft1", "ft1")]
        [InlineData("SEPAY-FT26ABC", "FT26ABC")]
        public void SePayBankReference_ReturnsThePartAfterThePrefix(string maGiaoDich, string expected)
        {
            Assert.Equal(expected, InvoicePaymentPolicy.SePayBankReference(maGiaoDich));
        }

        [Theory]
        [InlineData("SEPAY-ID-5")]
        [InlineData("sepay-id-92704")]
        [InlineData("GD-1")]
        [InlineData("SEPAY-")]
        [InlineData("SEPAY-   ")]
        [InlineData("")]
        [InlineData(null)]
        public void SePayBankReference_IsNull_ForTheIdKeyForeignKeysAndEmptyReferences(string? maGiaoDich)
        {
            Assert.Null(InvoicePaymentPolicy.SePayBankReference(maGiaoDich));
        }

        [Fact]
        public void SePayBankReference_RoundTripsTheKeyBuiltBySePayLedgerKey()
        {
            var key = InvoicePaymentPolicy.SePayLedgerKey(" ft 26 abc ");

            Assert.True(InvoicePaymentPolicy.IsSePayKey(key));
            Assert.Equal("FT26ABC", InvoicePaymentPolicy.SePayBankReference(key));
        }

        // ---------- Mã chuyển khoản dùng chung (QR, email, PDF) ----------

        [Theory]
        [InlineData(1, "TTPTHD1")]
        [InlineData(7, "TTPTHD7")]
        [InlineData(105, "TTPTHD105")]
        [InlineData(123456789, "TTPTHD123456789")]
        public void TransferCode_IsThePrefixFollowedByTheInvoiceId(int hoaDonId, string expected)
        {
            Assert.Equal(expected, InvoiceTransferHandler.TransferCode(hoaDonId));
        }

        [Theory]
        [InlineData(7)]
        [InlineData(105)]
        public void TransferCode_IsReadBackByTheParser(int hoaDonId)
        {
            var code = InvoiceTransferHandler.TransferCode(hoaDonId);

            Assert.Equal(code, TransferCodeParser.Extract(code, null, null));
            Assert.Equal(code, TransferCodeParser.Extract(null, $"Nguyen Van A {code} tien phong", null));
        }
    }
}
