using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using QuanLyChoThuePhongTroWeb.Application.Common.Configurations;
using QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.ExternalServices.Payments
{
    // Xác thực webhook SePay (spec thanh toán trực tuyến §4.1, bước 2). So sánh bằng FixedTimeEquals.
    // Đọc SePayOptions mỗi lần gọi để cấu hình đổi lúc chạy (ví dụ trong test) có hiệu lực ngay.
    public class SePayWebhookVerifier : ISePayWebhookVerifier
    {
        private const string SignaturePrefix = "sha256=";
        private const string ApiKeyPrefix = "Apikey ";

        private readonly SePayOptions _options;
        private readonly TimeProvider _time;

        public SePayWebhookVerifier(SePayOptions options, TimeProvider time)
        {
            _options = options;
            _time = time;
        }

        public SePayWebhookVerification Verify(byte[] rawBody, string? timestampHeader, string? signatureHeader, string? authorizationHeader)
        {
            return _options.WebhookAuthMode switch
            {
                SePayWebhookAuthMode.Hmac => VerifyHmac(rawBody, timestampHeader, signatureHeader),
                SePayWebhookAuthMode.ApiKey => VerifyApiKey(authorizationHeader),
                _ => Invalid("Chế độ xác thực webhook không hợp lệ")
            };
        }

        // Chữ ký = "sha256=" + hex thường của HMAC-SHA256(key = WebhookSecret, data = "{timestamp}." + body).
        private SePayWebhookVerification VerifyHmac(byte[] rawBody, string? timestampHeader, string? signatureHeader)
        {
            if (string.IsNullOrWhiteSpace(_options.WebhookSecret))
            {
                return Invalid("Chưa cấu hình khóa bí mật của webhook");
            }

            if (string.IsNullOrWhiteSpace(timestampHeader) ||
                !long.TryParse(timestampHeader, NumberStyles.None, CultureInfo.InvariantCulture, out var timestamp))
            {
                return Invalid("Thiếu hoặc sai định dạng timestamp");
            }

            var now = _time.GetUtcNow().ToUnixTimeSeconds();
            if (Math.Abs(now - timestamp) > _options.TimestampToleranceSeconds)
            {
                return Invalid("Lệch thời gian");
            }

            var signature = (signatureHeader ?? string.Empty).Trim().ToLowerInvariant();
            if (!signature.StartsWith(SignaturePrefix, StringComparison.Ordinal))
            {
                return Invalid("Thiếu hoặc sai định dạng chữ ký");
            }

            var timestampBytes = Encoding.UTF8.GetBytes(timestampHeader + ".");
            var data = new byte[timestampBytes.Length + rawBody.Length];
            timestampBytes.CopyTo(data, 0);
            rawBody.CopyTo(data, timestampBytes.Length);
            var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(_options.WebhookSecret), data);
            var expected = SignaturePrefix + Convert.ToHexString(hash).ToLowerInvariant();

            return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(signature))
                ? new SePayWebhookVerification(true, null)
                : Invalid("Chữ ký không khớp");
        }

        private SePayWebhookVerification VerifyApiKey(string? authorizationHeader)
        {
            if (string.IsNullOrWhiteSpace(_options.WebhookApiKey))
            {
                return Invalid("Chưa cấu hình API key của webhook");
            }

            var expected = Encoding.UTF8.GetBytes(ApiKeyPrefix + _options.WebhookApiKey);
            var actual = Encoding.UTF8.GetBytes(authorizationHeader ?? string.Empty);

            return CryptographicOperations.FixedTimeEquals(expected, actual)
                ? new SePayWebhookVerification(true, null)
                : Invalid("API key không khớp");
        }

        private static SePayWebhookVerification Invalid(string reason) => new(false, reason);
    }
}
