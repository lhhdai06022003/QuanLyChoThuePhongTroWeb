using System;
using QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    // Khóa chống trùng theo kênh (spec VNPay §6): mặc định vẫn là SePay, kênh VNPay dùng VNPAY-{vnp_TransactionNo}.
    public class IncomingBankTransferVnPayTests
    {
        private static IncomingBankTransfer Transfer(string? referenceCode, OnlinePaymentChannel? kenh = null)
        {
            var transfer = new IncomingBankTransfer(
                0, "NCB", new DateTime(2026, 10, 3, 2, 30, 0, DateTimeKind.Utc), null, null, null,
                "Thanh toan hoa don HD-7", "in", 2_000_000m, referenceCode, "HD7_1");
            return kenh.HasValue ? transfer with { Kenh = kenh.Value } : transfer;
        }

        [Fact]
        public void Channel_DefaultsToSePay_AndKeepsTheSePayDedupKey()
        {
            var transfer = Transfer("ft26abc");

            Assert.Equal(OnlinePaymentChannel.SePay, transfer.Kenh);
            Assert.Null(transfer.CardType);
            Assert.Equal("SEPAY-FT26ABC", transfer.DedupKey);
        }

        [Theory]
        [InlineData(" 14123456 ", "VNPAY-14123456")]
        [InlineData("ab c", "VNPAY-ABC")]
        [InlineData("14123456", "VNPAY-14123456")]
        public void DedupKey_ForVnPay_IsUpperCaseWithoutWhitespace(string referenceCode, string expected)
        {
            Assert.Equal(expected, Transfer(referenceCode, OnlinePaymentChannel.VnPay).DedupKey);
        }
    }
}
