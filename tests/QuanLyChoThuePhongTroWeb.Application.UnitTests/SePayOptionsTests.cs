using System;
using QuanLyChoThuePhongTroWeb.Application.Common.Configurations;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    public class SePayOptionsTests
    {
        [Fact]
        public void Defaults_AreValid_AndWebhookIsDisabled()
        {
            var options = new SePayOptions();

            options.Validate();

            Assert.False(options.Enabled);
            Assert.Equal(SePayWebhookAuthMode.Hmac, options.WebhookAuthMode);
            Assert.Equal(300, options.TimestampToleranceSeconds);
            Assert.Equal("https://userapi.sepay.vn/v2", options.ApiBaseUrl);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void Enabled_Hmac_WithoutSecret_Throws(string secret)
        {
            var ex = Assert.Throws<InvalidOperationException>(() =>
                new SePayOptions { Enabled = true, WebhookAuthMode = SePayWebhookAuthMode.Hmac, WebhookSecret = secret }.Validate());

            Assert.Contains("SePay:WebhookSecret", ex.Message);
        }

        [Fact]
        public void Enabled_Hmac_WithSecret_IsValid_EvenWithoutApiKey()
        {
            new SePayOptions { Enabled = true, WebhookSecret = "secret" }.Validate();
        }

        [Fact]
        public void Enabled_ApiKey_WithoutKey_Throws()
        {
            var ex = Assert.Throws<InvalidOperationException>(() =>
                new SePayOptions { Enabled = true, WebhookAuthMode = SePayWebhookAuthMode.ApiKey, WebhookApiKey = string.Empty, WebhookSecret = "secret" }.Validate());

            Assert.Contains("SePay:WebhookApiKey", ex.Message);
        }

        [Fact]
        public void Enabled_ApiKey_WithKey_IsValid_EvenWithoutSecret()
        {
            new SePayOptions { Enabled = true, WebhookAuthMode = SePayWebhookAuthMode.ApiKey, WebhookApiKey = "key" }.Validate();
        }

        [Fact]
        public void Disabled_WithoutSecretOrKey_IsValid()
        {
            new SePayOptions { Enabled = false, WebhookSecret = string.Empty, WebhookApiKey = string.Empty }.Validate();
            new SePayOptions { Enabled = false, WebhookAuthMode = SePayWebhookAuthMode.ApiKey, WebhookApiKey = string.Empty }.Validate();
        }

        [Fact]
        public void UndefinedAuthMode_Throws()
        {
            var ex = Assert.Throws<InvalidOperationException>(() =>
                new SePayOptions { WebhookAuthMode = (SePayWebhookAuthMode)7 }.Validate());

            Assert.Contains("SePay:WebhookAuthMode", ex.Message);
        }

        [Theory]
        [InlineData(29)]
        [InlineData(901)]
        [InlineData(0)]
        [InlineData(-1)]
        public void TimestampTolerance_OutOfRange_Throws(int seconds)
        {
            var ex = Assert.Throws<InvalidOperationException>(() =>
                new SePayOptions { TimestampToleranceSeconds = seconds }.Validate());

            Assert.Contains("SePay:TimestampToleranceSeconds", ex.Message);
        }

        [Theory]
        [InlineData(30)]
        [InlineData(900)]
        public void TimestampTolerance_Boundaries_AreValid(int seconds)
        {
            new SePayOptions { TimestampToleranceSeconds = seconds }.Validate();
        }

        [Theory]
        [InlineData("http://userapi.sepay.vn/v2")]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("/v2")]
        [InlineData("userapi.sepay.vn/v2")]
        [InlineData("ftp://userapi.sepay.vn/v2")]
        public void ApiBaseUrl_NotAbsoluteHttps_Throws(string url)
        {
            var ex = Assert.Throws<InvalidOperationException>(() => new SePayOptions { ApiBaseUrl = url }.Validate());

            Assert.Contains("SePay:ApiBaseUrl", ex.Message);
        }

        [Fact]
        public void ApiBaseUrl_Https_IsValid()
        {
            new SePayOptions { ApiBaseUrl = "https://userapi-sandbox.sepay.vn/v2" }.Validate();
        }

        [Fact]
        public void ApiBaseUrl_IsChecked_EvenWhenDisabled()
        {
            Assert.Throws<InvalidOperationException>(() => new SePayOptions { Enabled = false, ApiBaseUrl = "http://x" }.Validate());
        }
    }
}
