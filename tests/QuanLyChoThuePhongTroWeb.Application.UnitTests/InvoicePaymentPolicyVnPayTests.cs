using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Payments;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    // Khóa và nhận diện dòng sổ do VNPay ghi (spec VNPay §6) và các hàm chung với dòng SePay.
    public class InvoicePaymentPolicyVnPayTests
    {
        [Theory]
        [InlineData("14123456", "VNPAY-14123456")]
        [InlineData(" ab 12 ", "VNPAY-AB12")]
        public void VnPayLedgerKey_IsPrefixedUpperCaseWithoutWhitespace(string transactionNo, string expected)
        {
            Assert.Equal(expected, InvoicePaymentPolicy.VnPayLedgerKey(transactionNo));
        }

        [Theory]
        [InlineData("VNPAY-1", true)]
        [InlineData("vnpay-1", true)]
        [InlineData("SEPAY-1", false)]
        [InlineData("GD-1", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void IsVnPayKey_MatchesThePrefixIgnoringCase(string? maGiaoDich, bool expected)
        {
            Assert.Equal(expected, InvoicePaymentPolicy.IsVnPayKey(maGiaoDich));
        }

        [Fact]
        public void IsSePayKey_DoesNotMatchAVnPayKey()
        {
            Assert.False(InvoicePaymentPolicy.IsSePayKey("VNPAY-1"));
        }

        [Theory]
        [InlineData("VNPAY-9", "9")]
        [InlineData("vnpay-14123456", "14123456")]
        [InlineData("VNPAY-", null)]
        [InlineData("SEPAY-X", null)]
        [InlineData("TM-1", null)]
        [InlineData(null, null)]
        public void VnPayReference_ReturnsThePartAfterThePrefix(string? maGiaoDich, string? expected)
        {
            Assert.Equal(expected, InvoicePaymentPolicy.VnPayReference(maGiaoDich));
        }

        [Theory]
        [InlineData("SEPAY-FT1", "FT1")]
        [InlineData("SEPAY-ID-5", null)]
        [InlineData("VNPAY-9", "9")]
        [InlineData("TM-1", null)]
        [InlineData(null, null)]
        public void AutoPaymentReference_CoversBothChannels(string? maGiaoDich, string? expected)
        {
            Assert.Equal(expected, InvoicePaymentPolicy.AutoPaymentReference(maGiaoDich));
        }

        [Theory]
        [InlineData("SEPAY-FT1", true)]
        [InlineData("SEPAY-ID-5", true)]
        [InlineData("VNPAY-9", true)]
        [InlineData("TM-1", false)]
        [InlineData("GD-OLD", false)]
        [InlineData(null, false)]
        public void IsSystemRecordedKey_CoversBothChannels(string? maGiaoDich, bool expected)
        {
            Assert.Equal(expected, InvoicePaymentPolicy.IsSystemRecordedKey(maGiaoDich));
        }
    }
}
