using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using QuanLyChoThuePhongTroWeb.Application.Common.Configurations;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.Configurations
{
    public class MeterImageOptionsTests
    {
        [Fact]
        public void Bind_MeterImageOptions_DeduplicatesAllowedContentTypes()
        {
            var configData = new Dictionary<string, string?>
            {
                { "MeterImageOptions:AllowedContentTypes:0", "image/jpeg" },
                { "MeterImageOptions:AllowedContentTypes:1", "image/png" },
                { "MeterImageOptions:AllowedContentTypes:2", "image/jpeg" }
            };
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(configData).Build();

            var options = configuration.GetSection("MeterImageOptions").Get<MeterImageOptions>() ?? new MeterImageOptions();
            options.Validate();

            Assert.Equal(3, options.AllowedContentTypes.Count); // image/jpeg, image/png, image/webp (mặc định) không bị lặp
            Assert.Single(options.AllowedContentTypes, ct => ct.Equals("image/jpeg", StringComparison.OrdinalIgnoreCase));
        }

        [Theory]
        [InlineData(-1, 15, 10)]
        [InlineData(0, 15, 10)]
        [InlineData(1000, 0, 10)]
        [InlineData(1000, -5, 10)]
        [InlineData(1000, 15, 0)]
        [InlineData(1000, 15, -1)]
        public void Validate_WhenValuesNotGreaterThanZero_ThrowsInvalidOperationException(long maxBytes, int timeoutSec, int staleMin)
        {
            var options = new MeterImageOptions
            {
                MaxFileSizeBytes = maxBytes,
                DownloadTimeoutSeconds = timeoutSec,
                StaleProcessingMinutes = staleMin
            };

            Assert.Throws<InvalidOperationException>(() => options.Validate());
        }
    }
}
