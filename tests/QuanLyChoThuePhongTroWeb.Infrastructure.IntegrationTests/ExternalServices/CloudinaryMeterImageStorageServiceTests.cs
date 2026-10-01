using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using QuanLyChoThuePhongTroWeb.Application.Common.Configurations;
using QuanLyChoThuePhongTroWeb.Application.Common.Files;
using QuanLyChoThuePhongTroWeb.Infrastructure.ExternalServices.Storage;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.ExternalServices
{
    public class CloudinaryMeterImageStorageServiceTests
    {
        private class FakeHttpMessageHandler : HttpMessageHandler
        {
            public Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> Handler { get; set; } = null!;

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return Handler(request, cancellationToken);
            }
        }

        private static CloudinaryMeterImageStorageService CreateService(
            HttpClient? httpClient = null,
            IConfiguration? configuration = null,
            MeterImageOptions? options = null,
            ILogger<CloudinaryMeterImageStorageService>? logger = null)
        {
            var inMemorySettings = new System.Collections.Generic.Dictionary<string, string?>
            {
                {"Cloudinary:CloudName", "test-cloud"},
                {"Cloudinary:ApiKey", "fake_key"},
                {"Cloudinary:ApiSecret", "fake_secret"}
            };
            var config = configuration ?? new ConfigurationBuilder().AddInMemoryCollection(inMemorySettings).Build();
            var client = httpClient ?? new HttpClient();
            var opt = options ?? new MeterImageOptions();
            var log = logger ?? NullLogger<CloudinaryMeterImageStorageService>.Instance;
            return new CloudinaryMeterImageStorageService(config, client, opt, log);
        }

        [Fact]
        public async Task UploadAsync_WhenCredentialsMissing_ThrowsInvalidOperationException()
        {
            var emptyConfig = new ConfigurationBuilder().AddInMemoryCollection().Build();
            var service = CreateService(configuration: emptyConfig);

            using var stream = new MemoryStream(new byte[] { 1, 2, 3 });
            var uploadFile = new UploadFile(stream, "meter.jpg", "image/jpeg", 3);

            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await service.UploadAsync(uploadFile, "meters");
            });
        }

        [Fact]
        public async Task ReadAsync_WhenUrlValid_ReturnsBytesAndContentType()
        {
            var expectedBytes = new byte[] { 10, 20, 30, 40 };

            var handler = new FakeHttpMessageHandler
            {
                Handler = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(expectedBytes)
                    {
                        Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg") }
                    }
                })
            };

            var service = CreateService(httpClient: new HttpClient(handler));
            var result = await service.ReadAsync("public_id_123", "https://res.cloudinary.com/test-cloud/image/upload/sample.jpg");

            Assert.NotNull(result);
            Assert.Equal(expectedBytes, result.Bytes);
            Assert.Equal("image/jpeg", result.ContentType);
        }

        [Fact]
        public async Task ReadAsync_WhenUrlEmpty_BuildsUrlFromPublicId()
        {
            var expectedBytes = new byte[] { 1, 2, 3 };
            HttpRequestMessage? sentRequest = null;

            var handler = new FakeHttpMessageHandler
            {
                Handler = (req, _) =>
                {
                    sentRequest = req;
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new ByteArrayContent(expectedBytes)
                        {
                            Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg") }
                        }
                    });
                }
            };

            var service = CreateService(httpClient: new HttpClient(handler));
            var result = await service.ReadAsync("meters/sample_id", url: string.Empty);

            Assert.NotNull(result);
            Assert.NotNull(sentRequest);
            Assert.Contains("res.cloudinary.com", sentRequest.RequestUri?.ToString());
            Assert.Contains("test-cloud", sentRequest.RequestUri?.ToString());
        }

        [Fact]
        public async Task ReadAsync_WhenHttpError_ThrowsInvalidOperationException()
        {
            var handler = new FakeHttpMessageHandler
            {
                Handler = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound))
            };

            var service = CreateService(httpClient: new HttpClient(handler));

            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await service.ReadAsync("public_id_123", "https://res.cloudinary.com/test-cloud/image/upload/sample.jpg");
            });
        }

        [Fact]
        public async Task ReadAsync_WhenHttpClientTimesOut_ThrowsInvalidOperationException()
        {
            var handler = new FakeHttpMessageHandler
            {
                Handler = (_, _) => throw new TaskCanceledException("HttpClient timeout")
            };

            var service = CreateService(httpClient: new HttpClient(handler));

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.ReadAsync("public_id_123", "https://res.cloudinary.com/test-cloud/image/upload/sample.jpg"));
            Assert.Contains("thời gian chờ", ex.Message);
        }

        [Fact]
        public async Task DeleteAsync_WhenCredentialsMissing_CompletesWithoutFatalCrash()
        {
            var emptyConfig = new ConfigurationBuilder().AddInMemoryCollection().Build();
            var service = CreateService(configuration: emptyConfig);

            var exception = await Record.ExceptionAsync(() => service.DeleteAsync("sample_public_id"));
            Assert.Null(exception);
        }

        [Fact]
        public async Task ReadAsync_WhenCallerCanceled_ThrowsOperationCanceledException()
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            var handler = new FakeHttpMessageHandler
            {
                Handler = (_, token) =>
                {
                    token.ThrowIfCancellationRequested();
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
                }
            };

            var service = CreateService(httpClient: new HttpClient(handler));

            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            {
                await service.ReadAsync("public_id_123", "https://res.cloudinary.com/test-cloud/image/upload/sample.jpg", cts.Token);
            });
        }

        [Theory]
        [InlineData("http://res.cloudinary.com/test-cloud/image/upload/sample.jpg")] // Not HTTPS
        [InlineData("https://evil.com/image.jpg")] // Wrong host
        [InlineData("https://evilcloudinary.com/test-cloud/image.jpg")] // Subdomain trick
        [InlineData("https://res.cloudinary.com.evil.com/test-cloud/image.jpg")] // Domain suffix trick
        [InlineData("https://localhost/test-cloud/image.jpg")] // Localhost rejected
        [InlineData("https://res.cloudinary.com/other-cloud/image.jpg")] // Other cloud rejected
        [InlineData("not-a-url")]
        public async Task ReadAsync_WhenNonHttpsOrWrongHostOrWrongCloud_ThrowsInvalidOperationException(string badUrl)
        {
            var service = CreateService();

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.ReadAsync("public_id", badUrl));
        }

        [Fact]
        public async Task ReadAsync_WhenContentLengthExceedsConfiguredMax_ThrowsInvalidOperationException()
        {
            var handler = new FakeHttpMessageHandler
            {
                Handler = (_, _) =>
                {
                    var response = new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new ByteArrayContent(new byte[] { 1, 2 })
                    };
                    response.Content.Headers.ContentLength = 6 * 1024 * 1024; // 6MB header
                    response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
                    return Task.FromResult(response);
                }
            };

            var service = CreateService(httpClient: new HttpClient(handler));

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.ReadAsync("pub1", "https://res.cloudinary.com/test-cloud/image/upload/sample.jpg"));
            Assert.Contains("vượt quá giới hạn", ex.Message);
        }

        [Fact]
        public async Task ReadAsync_WhenStreamingExceedsCustomConfiguredMax_ThrowsInvalidOperationException()
        {
            var customOptions = new MeterImageOptions
            {
                MaxFileSizeBytes = 100 // Custom limit 100 bytes
            };

            var handler = new FakeHttpMessageHandler
            {
                Handler = (_, _) =>
                {
                    var stream = new MemoryStream(new byte[150]);
                    var content = new StreamContent(stream);
                    content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
                    content.Headers.ContentLength = null;
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
                }
            };

            var service = CreateService(httpClient: new HttpClient(handler), options: customOptions);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.ReadAsync("pub1", "https://res.cloudinary.com/test-cloud/image/upload/sample.jpg"));
            Assert.Contains("vượt quá giới hạn", ex.Message);
        }

        [Fact]
        public async Task ReadAsync_WhenInvalidContentType_ThrowsInvalidOperationException()
        {
            var handler = new FakeHttpMessageHandler
            {
                Handler = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(new byte[] { 1, 2 })
                    {
                        Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf") }
                    }
                })
            };

            var service = CreateService(httpClient: new HttpClient(handler));

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.ReadAsync("pub1", "https://res.cloudinary.com/test-cloud/image/upload/sample.jpg"));
            Assert.Contains("không được hỗ trợ", ex.Message);
        }
    }
}
