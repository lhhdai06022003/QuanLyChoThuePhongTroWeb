using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    // Chuỗi ký, chữ ký HMAC-SHA512, mã đơn và giờ của VNPay (spec VNPay §5.3, §5.4). Hàm thuần.
    public class VnPayProtocolTests
    {
        private const string Secret = "test-vnpay-hash-secret";

        private static Dictionary<string, string> Signed()
        {
            var parameters = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["vnp_Amount"] = "200000000",
                ["vnp_TxnRef"] = "HD7_638654321098765432",
                ["vnp_OrderInfo"] = "Thanh toan hoa don HD-7",
                ["vnp_ResponseCode"] = "00"
            };
            parameters["vnp_SecureHash"] = VnPayProtocol.Sign(VnPayProtocol.BuildSignData(parameters), Secret);
            return parameters;
        }

        // ---------- BuildSignData ----------

        [Fact]
        public void BuildSignData_KeepsOnlyNonEmptyVnpParams_SortsThem_AndUrlEncodesValues()
        {
            var parameters = new Dictionary<string, string>
            {
                ["vnp_TxnRef"] = "HD7_1",
                ["vnp_Amount"] = "200000000",
                ["vnp_OrderInfo"] = "Thanh toan hoa don HD-7",
                ["vnp_ReturnUrl"] = "https://tro.test/a?b=c",
                ["vnp_BankCode"] = "",
                ["vnp_SecureHash"] = "x",
                ["vnp_SecureHashType"] = "HmacSHA512",
                ["other"] = "1"
            };

            Assert.Equal(
                "vnp_Amount=200000000&vnp_OrderInfo=Thanh+toan+hoa+don+HD-7&vnp_ReturnUrl=https%3A%2F%2Ftro.test%2Fa%3Fb%3Dc&vnp_TxnRef=HD7_1",
                VnPayProtocol.BuildSignData(parameters));
        }

        [Fact]
        public void BuildSignData_SortsByOrdinalKeyOrder()
        {
            var parameters = new Dictionary<string, string> { ["vnp_a"] = "1", ["vnp_B"] = "2" };

            Assert.Equal("vnp_B=2&vnp_a=1", VnPayProtocol.BuildSignData(parameters));
        }

        // ---------- Sign / Verify ----------

        [Fact]
        public void Sign_IsHmacSha512Hex_InLowerCase()
        {
            const string data = "vnp_Amount=100&vnp_TxnRef=HD1_1";

            var signature = VnPayProtocol.Sign(data, Secret);

            var expected = Convert.ToHexString(HMACSHA512.HashData(Encoding.UTF8.GetBytes(Secret), Encoding.UTF8.GetBytes(data))).ToLowerInvariant();
            Assert.Equal(expected, signature);
            Assert.Matches(new Regex("^[0-9a-f]{128}$"), signature);
        }

        [Fact]
        public void Verify_ValidSignature_ReturnsTrue()
        {
            Assert.True(VnPayProtocol.Verify(Signed(), Secret));
        }

        [Fact]
        public void Verify_IgnoresTheSecureHashTypeParameter()
        {
            var parameters = Signed();
            parameters["vnp_SecureHashType"] = "HmacSHA512";

            Assert.True(VnPayProtocol.Verify(parameters, Secret));
        }

        [Fact]
        public void Verify_UpperCaseSignature_ReturnsTrue()
        {
            var parameters = Signed();
            parameters["vnp_SecureHash"] = parameters["vnp_SecureHash"].ToUpperInvariant();

            Assert.True(VnPayProtocol.Verify(parameters, Secret));
        }

        [Fact]
        public void Verify_OneCharacterChanged_ReturnsFalse()
        {
            var parameters = Signed();
            var hash = parameters["vnp_SecureHash"];
            parameters["vnp_SecureHash"] = (hash[0] == '0' ? '1' : '0') + hash[1..];

            Assert.False(VnPayProtocol.Verify(parameters, Secret));
        }

        [Fact]
        public void Verify_AmountChangedAfterSigning_ReturnsFalse()
        {
            var parameters = Signed();
            parameters["vnp_Amount"] = "100";

            Assert.False(VnPayProtocol.Verify(parameters, Secret));
        }

        [Fact]
        public void Verify_WrongSecret_ReturnsFalse()
        {
            Assert.False(VnPayProtocol.Verify(Signed(), "another-secret"));
        }

        [Fact]
        public void Verify_MissingOrEmptySignature_ReturnsFalse()
        {
            var missing = Signed();
            missing.Remove("vnp_SecureHash");
            var empty = Signed();
            empty["vnp_SecureHash"] = "";

            Assert.False(VnPayProtocol.Verify(missing, Secret));
            Assert.False(VnPayProtocol.Verify(empty, Secret));
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void Verify_BlankSecret_ReturnsFalse(string secret)
        {
            Assert.False(VnPayProtocol.Verify(Signed(), secret));
        }

        // ---------- Mã đơn ----------

        [Fact]
        public void TryParseTxnRef_ValidReference_ReturnsTheInvoiceId()
        {
            Assert.True(VnPayProtocol.TryParseTxnRef("HD105_638654321098765432", out var hoaDonId));
            Assert.Equal(105, hoaDonId);
        }

        [Theory]
        [InlineData("hd105_1")]
        [InlineData("XX105_1")]
        [InlineData("HD0_1")]
        [InlineData("HD1234567890_1")]
        [InlineData("HD105")]
        [InlineData("HD105_")]
        [InlineData("HD_1")]
        [InlineData("HD105_12a")]
        [InlineData("HD١_1")]
        [InlineData("HD105_1\n")]
        [InlineData(null)]
        [InlineData("")]
        public void TryParseTxnRef_InvalidReference_ReturnsFalse(string? txnRef)
        {
            Assert.False(VnPayProtocol.TryParseTxnRef(txnRef, out var hoaDonId));
            Assert.Equal(0, hoaDonId);
        }

        [Fact]
        public void BuildTxnRef_UsesTheTicks_AndRoundTrips()
        {
            var now = new DateTime(2026, 10, 3, 3, 0, 0, DateTimeKind.Utc);

            var txnRef = VnPayProtocol.BuildTxnRef(7, now);

            Assert.Equal($"HD7_{now.Ticks}", txnRef);
            Assert.True(VnPayProtocol.TryParseTxnRef(txnRef, out var hoaDonId));
            Assert.Equal(7, hoaDonId);
        }

        // ---------- Giờ ----------

        [Fact]
        public void FormatVietnamTime_AddsSevenHours()
        {
            Assert.Equal("20261003093000", VnPayProtocol.FormatVietnamTime(new DateTime(2026, 10, 3, 2, 30, 0, DateTimeKind.Utc)));
        }

        [Fact]
        public void TryParseVietnamTime_ConvertsToUtc()
        {
            Assert.True(VnPayProtocol.TryParseVietnamTime("20261003093000", out var utc));

            Assert.Equal(new DateTime(2026, 10, 3, 2, 30, 0), utc);
            Assert.Equal(DateTimeKind.Utc, utc.Kind);
        }

        [Fact]
        public void TryParseVietnamTime_BeforeSevenAm_FallsOnThePreviousUtcDay()
        {
            Assert.True(VnPayProtocol.TryParseVietnamTime("20261003050000", out var utc));

            Assert.Equal(new DateTime(2026, 10, 2, 22, 0, 0), utc);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("2026-10-03")]
        [InlineData("20261340000000")]
        public void TryParseVietnamTime_Invalid_ReturnsFalse(string? value)
        {
            Assert.False(VnPayProtocol.TryParseVietnamTime(value, out _));
        }
    }
}
