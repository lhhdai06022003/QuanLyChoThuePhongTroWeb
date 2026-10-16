using System;
using System.Text;
using QuanLyChoThuePhongTroWeb.Web.Helpers;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Web.IntegrationTests
{
    // Không dùng WebTestCollection: chỉ kiểm tra các helper thuần của Web.
    public class SePayWebhookPayloadParserTests
    {
        private const string RequiredOnly =
            "{\"id\":92704,\"transferType\":\"in\",\"transferAmount\":2000000,\"transactionDate\":\"2026-10-03 09:30:00\"}";

        private static bool TryParse(string json, out QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments.IncomingBankTransfer? transfer)
            => SePayWebhookPayloadParser.TryParse(Encoding.UTF8.GetBytes(json), out transfer);

        [Fact]
        public void FullPayload_IsMappedField_ByField()
        {
            const string json = "{\"id\":5000000000,\"gateway\":\"MBBank\",\"transactionDate\":\"2026-10-03 09:30:15\"," +
                "\"accountNumber\":\"0123456789\",\"subAccount\":\"VA01\",\"code\":\"TTPTHD105\"," +
                "\"content\":\"Nguyễn Văn A chuyển tiền TTPTHD105\",\"transferType\":\"in\",\"transferAmount\":2000000," +
                "\"accumulated\":19077000,\"referenceCode\":\"FT26ABC\",\"description\":\"BankAPINotify TTPTHD105\"}";

            Assert.True(TryParse(json, out var transfer));

            Assert.NotNull(transfer);
            Assert.Equal(5_000_000_000L, transfer!.SePayId);
            Assert.Equal("MBBank", transfer.Gateway);
            Assert.Equal(new DateTime(2026, 10, 3, 2, 30, 15), transfer.TransactionDateUtc);
            Assert.Equal(DateTimeKind.Utc, transfer.TransactionDateUtc.Kind);
            Assert.Equal("0123456789", transfer.AccountNumber);
            Assert.Equal("VA01", transfer.SubAccount);
            Assert.Equal("TTPTHD105", transfer.Code);
            Assert.Equal("Nguyễn Văn A chuyển tiền TTPTHD105", transfer.Content);
            Assert.Equal("in", transfer.TransferType);
            Assert.Equal(2_000_000m, transfer.TransferAmount);
            Assert.Equal("FT26ABC", transfer.ReferenceCode);
            Assert.Equal("BankAPINotify TTPTHD105", transfer.Description);
        }

        [Fact]
        public void RequiredFieldsOnly_LeavesOptionalFieldsNull()
        {
            Assert.True(TryParse(RequiredOnly, out var transfer));

            Assert.NotNull(transfer);
            Assert.Null(transfer!.Gateway);
            Assert.Null(transfer.AccountNumber);
            Assert.Null(transfer.SubAccount);
            Assert.Null(transfer.Code);
            Assert.Null(transfer.Content);
            Assert.Null(transfer.ReferenceCode);
            Assert.Null(transfer.Description);
            Assert.Equal("SEPAY-ID-92704", transfer.DedupKey);
        }

        [Fact]
        public void OptionalFieldsOfOtherTypes_AreTreatedAsMissing()
        {
            const string json = "{\"id\":1,\"transferType\":\"in\",\"transferAmount\":1000,\"transactionDate\":\"2026-10-03 09:30:00\"," +
                "\"gateway\":12,\"accountNumber\":null,\"subAccount\":{\"a\":1},\"code\":[1],\"content\":true," +
                "\"referenceCode\":1.5,\"description\":null,\"extra\":\"ignored\"}";

            Assert.True(TryParse(json, out var transfer));

            Assert.NotNull(transfer);
            Assert.Null(transfer!.Gateway);
            Assert.Null(transfer.AccountNumber);
            Assert.Null(transfer.SubAccount);
            Assert.Null(transfer.Code);
            Assert.Null(transfer.Content);
            Assert.Null(transfer.ReferenceCode);
            Assert.Null(transfer.Description);
        }

        [Theory]
        [InlineData("{\"transferType\":\"in\",\"transferAmount\":2000000,\"transactionDate\":\"2026-10-03 09:30:00\"}")]
        [InlineData("{\"id\":\"92704\",\"transferType\":\"in\",\"transferAmount\":2000000,\"transactionDate\":\"2026-10-03 09:30:00\"}")]
        [InlineData("{\"id\":92704.5,\"transferType\":\"in\",\"transferAmount\":2000000,\"transactionDate\":\"2026-10-03 09:30:00\"}")]
        [InlineData("{\"id\":92704,\"transferAmount\":2000000,\"transactionDate\":\"2026-10-03 09:30:00\"}")]
        [InlineData("{\"id\":92704,\"transferType\":null,\"transferAmount\":2000000,\"transactionDate\":\"2026-10-03 09:30:00\"}")]
        [InlineData("{\"id\":92704,\"transferType\":1,\"transferAmount\":2000000,\"transactionDate\":\"2026-10-03 09:30:00\"}")]
        [InlineData("{\"id\":92704,\"transferType\":\"in\",\"transactionDate\":\"2026-10-03 09:30:00\"}")]
        [InlineData("{\"id\":92704,\"transferType\":\"in\",\"transferAmount\":\"2000000\",\"transactionDate\":\"2026-10-03 09:30:00\"}")]
        [InlineData("{\"id\":92704,\"transferType\":\"in\",\"transferAmount\":2000000.5,\"transactionDate\":\"2026-10-03 09:30:00\"}")]
        [InlineData("{\"id\":92704,\"transferType\":\"in\",\"transferAmount\":2000000}")]
        [InlineData("{\"id\":92704,\"transferType\":\"in\",\"transferAmount\":2000000,\"transactionDate\":20261003}")]
        [InlineData("{\"Id\":92704,\"TransferType\":\"in\",\"TransferAmount\":2000000,\"TransactionDate\":\"2026-10-03 09:30:00\"}")]
        public void MissingOrWrongTypedRequiredField_IsRejected(string json)
        {
            Assert.False(TryParse(json, out var transfer));
            Assert.Null(transfer);
        }

        [Theory]
        [InlineData("")]
        [InlineData("{")]
        [InlineData("not json")]
        [InlineData("[]")]
        [InlineData("null")]
        [InlineData("\"text\"")]
        [InlineData("123")]
        [InlineData("{} trailing")]
        public void NotAJsonObject_IsRejected(string json)
        {
            Assert.False(TryParse(json, out var transfer));
            Assert.Null(transfer);
        }

        [Fact]
        public void InvalidUtf8_IsRejected_WithoutThrowing()
        {
            var body = Encoding.UTF8.GetBytes("{\"id\":1,\"transferType\":\"in\",\"transferAmount\":1000,\"transactionDate\":\"2026-10-03 09:30:00\",\"content\":\"xx\"}");
            // Byte 0xFF không bao giờ hợp lệ trong UTF-8; đặt vào ký tự đầu của giá trị "xx".
            body[^4] = 0xFF;

            Assert.False(SePayWebhookPayloadParser.TryParse(body, out var transfer));
            Assert.Null(transfer);
        }

        [Theory]
        [InlineData("2026-10-03 09:30:00", 2026, 10, 3, 2, 30, 0)]
        [InlineData("2026-01-01 00:30:00", 2025, 12, 31, 17, 30, 0)]
        [InlineData("2026-10-03 23:59:59", 2026, 10, 3, 16, 59, 59)]
        [InlineData("2026-03-01 06:59:59", 2026, 2, 28, 23, 59, 59)]
        [InlineData(" 2026-10-03 09:30:00 ", 2026, 10, 3, 2, 30, 0)]
        public void TransactionDate_IsVietnamTime_ConvertedToUtc(string value, int year, int month, int day, int hour, int minute, int second)
        {
            var json = RequiredOnly.Replace("2026-10-03 09:30:00", value);

            Assert.True(TryParse(json, out var transfer));

            Assert.Equal(new DateTime(year, month, day, hour, minute, second), transfer!.TransactionDateUtc);
            Assert.Equal(DateTimeKind.Utc, transfer.TransactionDateUtc.Kind);
        }

        [Theory]
        [InlineData("2026-10-03T09:30:00")]
        [InlineData("2026-10-03 09:30")]
        [InlineData("2026-10-03")]
        [InlineData("03/10/2026 09:30:00")]
        [InlineData("2026-13-01 00:00:00")]
        [InlineData("2026-02-30 09:30:00")]
        [InlineData("2026-10-03 24:00:00")]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("0001-01-01 00:00:00")]
        public void TransactionDate_InOtherFormatOrOutOfRange_IsRejected(string value)
        {
            var json = RequiredOnly.Replace("2026-10-03 09:30:00", value);

            Assert.False(TryParse(json, out var transfer));
            Assert.Null(transfer);
        }

        [Fact]
        public void TryParseExactToUtc_UsesOnlyTheGivenFormat()
        {
            Assert.True(VietnamTime.TryParseExactToUtc("03/10/2026 09:30", "dd/MM/yyyy HH:mm", out var utc));
            Assert.Equal(new DateTime(2026, 10, 3, 2, 30, 0), utc);
            Assert.Equal(DateTimeKind.Utc, utc.Kind);

            Assert.False(VietnamTime.TryParseExactToUtc("2026-10-03 09:30", "dd/MM/yyyy HH:mm", out _));
            Assert.False(VietnamTime.TryParseExactToUtc(null, "dd/MM/yyyy HH:mm", out _));
            Assert.False(VietnamTime.TryParseExactToUtc("  ", "dd/MM/yyyy HH:mm", out _));
        }

        [Fact]
        public void TryParseExactToUtc_AtTheLowerBound_ReturnsFalseInsteadOfThrowing()
        {
            Assert.False(VietnamTime.TryParseExactToUtc("0001-01-01 06:59:59", "yyyy-MM-dd HH:mm:ss", out _));
            Assert.True(VietnamTime.TryParseExactToUtc("0001-01-01 07:00:00", "yyyy-MM-dd HH:mm:ss", out var utc));
            Assert.Equal(DateTime.MinValue, utc);
        }

        [Fact]
        public void TryParseToUtc_ExistingBehaviour_IsUnchanged()
        {
            Assert.True(VietnamTime.TryParseToUtc("2026-10-03T09:30", out var utc));
            Assert.Equal(new DateTime(2026, 10, 3, 2, 30, 0), utc);
            Assert.False(VietnamTime.TryParseToUtc("2026-10-03 09:30:00.5", out _));
        }
    }
}
