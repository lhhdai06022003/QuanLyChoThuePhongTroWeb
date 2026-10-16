using System;
using QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    public class IncomingBankTransferTests
    {
        private static IncomingBankTransfer WithReference(string? referenceCode, long id = 92704) => new(
            id, "MBBank", new DateTime(2026, 10, 16, 3, 0, 0, DateTimeKind.Utc), "0123456789", null, null,
            "TTPTHD7", "in", 2_000_000m, referenceCode, null);

        [Theory]
        [InlineData("FT26ABC", "SEPAY-FT26ABC")]
        [InlineData("ft26abc", "SEPAY-FT26ABC")]
        [InlineData(" ft26 abc ", "SEPAY-FT26ABC")]
        public void DedupKey_WithReference_IsUpperCaseWithoutWhitespace(string referenceCode, string expected)
        {
            Assert.Equal(expected, WithReference(referenceCode).DedupKey);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void DedupKey_WithoutReference_UsesSePayId(string? referenceCode)
        {
            Assert.Equal("SEPAY-ID-92704", WithReference(referenceCode).DedupKey);
        }
    }
}
