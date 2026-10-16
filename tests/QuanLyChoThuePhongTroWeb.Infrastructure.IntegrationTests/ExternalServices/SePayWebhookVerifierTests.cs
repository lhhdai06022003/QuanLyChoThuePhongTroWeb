using System;
using System.Security.Cryptography;
using System.Text;
using QuanLyChoThuePhongTroWeb.Application.Common.Configurations;
using QuanLyChoThuePhongTroWeb.Infrastructure.ExternalServices.Payments;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.ExternalServices
{
    // Không dùng PostgreSqlFixture: chỉ kiểm tra chữ ký và API key của webhook SePay.
    public class SePayWebhookVerifierTests
    {
        private const string Secret = "test-sepay-webhook-secret";
        private const string ApiKey = "test-sepay-api-key";
        private static readonly DateTimeOffset Now = new(2026, 10, 16, 3, 0, 0, TimeSpan.Zero);
        private static readonly byte[] Body = Encoding.UTF8.GetBytes("{\"id\":92704,\"transferType\":\"in\",\"content\":\"Thanh toán TTPTHD105\"}");

        private sealed class FixedTimeProvider : TimeProvider
        {
            private readonly DateTimeOffset _now;
            public FixedTimeProvider(DateTimeOffset now) => _now = now;
            public override DateTimeOffset GetUtcNow() => _now;
        }

        private static SePayOptions HmacOptions() => new() { Enabled = true, WebhookAuthMode = SePayWebhookAuthMode.Hmac, WebhookSecret = Secret };

        private static SePayOptions ApiKeyOptions() => new() { Enabled = true, WebhookAuthMode = SePayWebhookAuthMode.ApiKey, WebhookApiKey = ApiKey };

        private static SePayWebhookVerifier NewVerifier(SePayOptions options) => new(options, new FixedTimeProvider(Now));

        // Tính độc lập với mã production: "sha256=" + hex thường của HMAC-SHA256(secret, "{timestamp}." + body).
        private static string Sign(string secret, string timestamp, byte[] body)
        {
            var prefix = Encoding.UTF8.GetBytes(timestamp + ".");
            var data = new byte[prefix.Length + body.Length];
            Buffer.BlockCopy(prefix, 0, data, 0, prefix.Length);
            Buffer.BlockCopy(body, 0, data, prefix.Length, body.Length);
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            return "sha256=" + Convert.ToHexString(hmac.ComputeHash(data)).ToLowerInvariant();
        }

        private static string Timestamp(int offsetSeconds = 0) => (Now.ToUnixTimeSeconds() + offsetSeconds).ToString();

        // ---------- Hmac ----------

        [Fact]
        public void Hmac_ValidSignature_IsValid()
        {
            var timestamp = Timestamp();

            var result = NewVerifier(HmacOptions()).Verify(Body, timestamp, Sign(Secret, timestamp, Body), null);

            Assert.True(result.IsValid);
            Assert.Null(result.Reason);
        }

        [Fact]
        public void Hmac_WrongSecret_IsInvalid()
        {
            var timestamp = Timestamp();

            var result = NewVerifier(HmacOptions()).Verify(Body, timestamp, Sign("other-secret", timestamp, Body), null);

            Assert.False(result.IsValid);
        }

        [Fact]
        public void Hmac_TamperedBody_IsInvalid()
        {
            var timestamp = Timestamp();
            var signature = Sign(Secret, timestamp, Body);
            var tampered = Encoding.UTF8.GetBytes("{\"id\":92704,\"transferType\":\"in\",\"content\":\"Thanh toán TTPTHD106\"}");

            var result = NewVerifier(HmacOptions()).Verify(tampered, timestamp, signature, null);

            Assert.False(result.IsValid);
        }

        [Fact]
        public void Hmac_SignatureForAnotherTimestamp_IsInvalid()
        {
            var signature = Sign(Secret, Timestamp(-10), Body);

            var result = NewVerifier(HmacOptions()).Verify(Body, Timestamp(), signature, null);

            Assert.False(result.IsValid);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Hmac_MissingSignature_IsInvalid(string? signature)
        {
            var result = NewVerifier(HmacOptions()).Verify(Body, Timestamp(), signature, null);

            Assert.False(result.IsValid);
        }

        [Fact]
        public void Hmac_SignatureWithoutPrefix_IsInvalid()
        {
            var timestamp = Timestamp();
            var signature = Sign(Secret, timestamp, Body)["sha256=".Length..];

            var result = NewVerifier(HmacOptions()).Verify(Body, timestamp, signature, null);

            Assert.False(result.IsValid);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("abc")]
        [InlineData("-5")]
        [InlineData("1.5")]
        [InlineData("1760583600 ")]
        public void Hmac_MissingOrMalformedTimestamp_IsInvalid(string? timestamp)
        {
            var signature = Sign(Secret, timestamp ?? string.Empty, Body);

            var result = NewVerifier(HmacOptions()).Verify(Body, timestamp, signature, null);

            Assert.False(result.IsValid);
        }

        [Theory]
        [InlineData(-300, true)]
        [InlineData(300, true)]
        [InlineData(-301, false)]
        [InlineData(301, false)]
        [InlineData(86400, false)]
        public void Hmac_TimestampSkew_IsLimitedByTolerance(int offsetSeconds, bool expectedValid)
        {
            var timestamp = Timestamp(offsetSeconds);

            var result = NewVerifier(HmacOptions()).Verify(Body, timestamp, Sign(Secret, timestamp, Body), null);

            Assert.Equal(expectedValid, result.IsValid);
            if (!expectedValid)
            {
                Assert.Contains("Lệch thời gian", result.Reason);
            }
        }

        [Fact]
        public void Hmac_ToleranceFollowsOptions()
        {
            var options = HmacOptions();
            options.TimestampToleranceSeconds = 30;
            var timestamp = Timestamp(-31);

            Assert.False(NewVerifier(options).Verify(Body, timestamp, Sign(Secret, timestamp, Body), null).IsValid);
        }

        [Fact]
        public void Hmac_ApiKeyHeaderDoesNotSubstituteForSignature()
        {
            var result = NewVerifier(HmacOptions()).Verify(Body, Timestamp(), null, "Apikey " + ApiKey);

            Assert.False(result.IsValid);
        }

        [Fact]
        public void Hmac_EmptySecret_IsAlwaysInvalid_EvenForASignatureMadeWithAnEmptyKey()
        {
            var options = HmacOptions();
            options.WebhookSecret = string.Empty;
            var timestamp = Timestamp();

            var result = NewVerifier(options).Verify(Body, timestamp, Sign(string.Empty, timestamp, Body), null);

            Assert.False(result.IsValid);
        }

        // ---------- ApiKey ----------

        [Fact]
        public void ApiKey_CorrectKey_IsValid()
        {
            var result = NewVerifier(ApiKeyOptions()).Verify(Body, null, null, "Apikey " + ApiKey);

            Assert.True(result.IsValid);
        }

        [Theory]
        [InlineData("Apikey wrong-key")]
        [InlineData("Bearer test-sepay-api-key")]
        [InlineData("test-sepay-api-key")]
        [InlineData("")]
        [InlineData(null)]
        public void ApiKey_WrongOrMissingHeader_IsInvalid(string? authorization)
        {
            var result = NewVerifier(ApiKeyOptions()).Verify(Body, null, null, authorization);

            Assert.False(result.IsValid);
        }

        [Fact]
        public void ApiKey_EmptyConfiguredKey_IsAlwaysInvalid()
        {
            var options = ApiKeyOptions();
            options.WebhookApiKey = string.Empty;

            Assert.False(NewVerifier(options).Verify(Body, null, null, "Apikey ").IsValid);
            Assert.False(NewVerifier(options).Verify(Body, null, null, string.Empty).IsValid);
        }

        [Fact]
        public void ApiKey_SignatureHeadersAreIgnored()
        {
            var timestamp = Timestamp();

            var result = NewVerifier(ApiKeyOptions()).Verify(Body, timestamp, Sign(Secret, timestamp, Body), null);

            Assert.False(result.IsValid);
        }

        // ---------- Chung ----------

        [Fact]
        public void Options_AreReadOnEveryCall()
        {
            var options = HmacOptions();
            var verifier = NewVerifier(options);
            var timestamp = Timestamp();
            var signature = Sign(Secret, timestamp, Body);
            Assert.True(verifier.Verify(Body, timestamp, signature, null).IsValid);

            options.WebhookAuthMode = SePayWebhookAuthMode.ApiKey;
            options.WebhookApiKey = ApiKey;

            Assert.False(verifier.Verify(Body, timestamp, signature, null).IsValid);
            Assert.True(verifier.Verify(Body, timestamp, signature, "Apikey " + ApiKey).IsValid);
        }

        [Fact]
        public void UndefinedAuthMode_IsInvalid()
        {
            var options = HmacOptions();
            options.WebhookAuthMode = (SePayWebhookAuthMode)9;
            var timestamp = Timestamp();

            Assert.False(NewVerifier(options).Verify(Body, timestamp, Sign(Secret, timestamp, Body), "Apikey " + ApiKey).IsValid);
        }

        [Fact]
        public void Reason_NeverContainsSecretsOrRequestData()
        {
            var timestamp = Timestamp();
            var wrongSignature = Sign("other-secret", timestamp, Body);

            var hmac = NewVerifier(HmacOptions()).Verify(Body, timestamp, wrongSignature, null);
            var apiKey = NewVerifier(ApiKeyOptions()).Verify(Body, null, null, "Apikey attacker-key");

            foreach (var reason in new[] { hmac.Reason, apiKey.Reason })
            {
                Assert.False(string.IsNullOrWhiteSpace(reason));
                Assert.DoesNotContain(Secret, reason);
                Assert.DoesNotContain(ApiKey, reason);
                Assert.DoesNotContain(wrongSignature, reason);
                Assert.DoesNotContain("attacker-key", reason);
                Assert.DoesNotContain("TTPTHD105", reason);
            }
        }
    }
}
