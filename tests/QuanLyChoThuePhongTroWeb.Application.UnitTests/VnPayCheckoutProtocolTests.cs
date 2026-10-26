using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    // Dựng URL thanh toán, nội dung đơn và bảng mô tả mã phản hồi VNPay (spec VNPay §4.2, §4.3, §5.1, §5.5).
    public class VnPayCheckoutProtocolTests
    {
        private const string Secret = "secret";
        private const string PaymentUrl = "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html";

        [Fact]
        public void BuildPaymentUrl_QueryIsTheSignData_FollowedByTheSignature_AndVerifiesBack()
        {
            var parameters = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["vnp_TmnCode"] = "TESTTMN1",
                ["vnp_Amount"] = "200000000",
                ["vnp_OrderInfo"] = "Thanh toan hoa don HD-7",
                ["vnp_ReturnUrl"] = "http://localhost:5009/KhachThue/HoaDon/ThanhToan/VnPay/KetQua"
            };

            var url = VnPayProtocol.BuildPaymentUrl(PaymentUrl, parameters, Secret);

            var signData = VnPayProtocol.BuildSignData(parameters);
            Assert.StartsWith(PaymentUrl + "?" + signData + "&vnp_SecureHash=", url);
            // Dấu cách của vnp_OrderInfo thành '+', không phải %20.
            Assert.Contains("vnp_OrderInfo=Thanh+toan+hoa+don+HD-7", url);

            var signature = url[(url.LastIndexOf("vnp_SecureHash=", StringComparison.Ordinal) + "vnp_SecureHash=".Length)..];
            Assert.Equal(128, signature.Length);
            Assert.Equal(VnPayProtocol.Sign(signData, Secret), signature);

            var query = url[(url.IndexOf('?') + 1)..].Split('&')
                .Select(pair => pair.Split('=', 2))
                .ToDictionary(p => p[0], p => WebUtility.UrlDecode(p[1]), StringComparer.Ordinal);
            Assert.True(VnPayProtocol.Verify(query, Secret));
            Assert.False(VnPayProtocol.Verify(query, "other-secret"));
        }

        [Theory]
        [InlineData("HD-7", "Thanh toan hoa don HD-7")]
        [InlineData("HĐ-10/2026 #1", "Thanh toan hoa don HD-102026 1")]
        [InlineData("Hóa đơn   Tháng_10", "Thanh toan hoa don Hoa don Thang_10")]
        [InlineData("Passss 1", "Thanh toan hoa don Passss 1")]
        [InlineData("", "Thanh toan hoa don")]
        [InlineData(null, "Thanh toan hoa don")]
        [InlineData("#/&=?", "Thanh toan hoa don")]
        public void BuildOrderInfo_RemovesDiacriticsAndSpecialCharacters(string? maHoaDon, string expected)
        {
            Assert.Equal(expected, VnPayProtocol.BuildOrderInfo(maHoaDon));
        }

        [Fact]
        public void BuildOrderInfo_IsCutAtTheVnPayLimit()
        {
            var info = VnPayProtocol.BuildOrderInfo(new string('A', 400));

            Assert.Equal(VnPayProtocol.MaxOrderInfoLength, info.Length);
            Assert.StartsWith("Thanh toan hoa don AAA", info);
        }

        [Theory]
        [InlineData("07", "mã 07")]
        [InlineData("09", "InternetBanking")]
        [InlineData("10", "quá 3 lần")]
        [InlineData("11", "hết hạn")]
        [InlineData("12", "bị khóa")]
        [InlineData("13", "OTP")]
        [InlineData("24", "hủy giao dịch")]
        [InlineData("51", "không đủ số dư")]
        [InlineData("65", "hạn mức")]
        [InlineData("75", "bảo trì")]
        [InlineData("79", "quá số lần quy định")]
        [InlineData("99", "Lỗi khác")]
        public void Describe_KnownCodes_ReturnTheVietnameseDescription(string code, string expectedFragment)
        {
            Assert.Contains(expectedFragment, VnPayResponseCodes.Describe(code));
        }

        [Fact]
        public void Describe_Code07_TellsTheCustomerToContactTheLandlord()
        {
            Assert.Equal(
                "Giao dịch đang được VNPay kiểm tra (mã 07). Vui lòng liên hệ chủ trọ trước khi thanh toán lại.",
                VnPayResponseCodes.Describe("07"));
        }

        [Theory]
        [InlineData("42", "Giao dịch không thành công (mã 42)")]
        [InlineData(" 42 ", "Giao dịch không thành công (mã 42)")]
        [InlineData(null, "Giao dịch không thành công.")]
        [InlineData("", "Giao dịch không thành công.")]
        public void Describe_UnknownOrMissingCode_FallsBack(string? code, string expected)
        {
            Assert.Equal(expected, VnPayResponseCodes.Describe(code));
        }
    }
}
