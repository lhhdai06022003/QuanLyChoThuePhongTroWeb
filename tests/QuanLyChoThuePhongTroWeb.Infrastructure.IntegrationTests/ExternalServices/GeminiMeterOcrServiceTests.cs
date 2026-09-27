using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.DTOs;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using QuanLyChoThuePhongTroWeb.Infrastructure.ExternalServices.AiAssistants;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.ExternalServices
{
    public class GeminiMeterOcrServiceTests
    {
        private class FakeHttpMessageHandler : HttpMessageHandler
        {
            public Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> Handler { get; set; } = null!;
            public HttpRequestMessage? LastRequest { get; private set; }
            public string? LastRequestBody { get; private set; }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                LastRequest = request;
                if (request.Content != null)
                {
                    LastRequestBody = await request.Content.ReadAsStringAsync(cancellationToken);
                }
                return await Handler(request, cancellationToken);
            }
        }

        private static GeminiMeterOcrService CreateService(FakeHttpMessageHandler handler)
        {
            var httpClient = new HttpClient(handler);
            var inMemorySettings = new System.Collections.Generic.Dictionary<string, string?>
            {
                {"Gemini:ApiKey", "fake_test_key"},
                {"Gemini:Model", "gemini-1.5-flash"}
            };
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(inMemorySettings)
                .Build();

            return new GeminiMeterOcrService(httpClient, configuration);
        }

        private static string CreateGeminiResponseJson(string rawText)
        {
            var responseObj = new
            {
                candidates = new[]
                {
                    new
                    {
                        content = new
                        {
                            parts = new[]
                            {
                                new { text = rawText }
                            }
                        }
                    }
                }
            };
            return JsonSerializer.Serialize(responseObj);
        }

        [Fact]
        public async Task ProcessImageAsync_WhenImageClear_ReturnsReadableResultWithCorrectValueAndConfidence()
        {
            var jsonFromAi = "{\"status\": \"readable\", \"suggested_value\": 125.5, \"confidence\": 0.95}";
            var fakeResponse = CreateGeminiResponseJson(jsonFromAi);

            var handler = new FakeHttpMessageHandler
            {
                Handler = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(fakeResponse, Encoding.UTF8, "application/json")
                })
            };
            var service = CreateService(handler);

            var result = await service.ProcessImageAsync(new byte[] { 1, 2, 3 }, "image/jpeg", LoaiDongHo.Dien);

            Assert.Equal(MeterOcrStatus.Readable, result.Status);
            Assert.Equal(125.5m, result.SuggestedValue);
            Assert.Equal(0.95, result.Confidence);
        }

        [Fact]
        public async Task ProcessImageAsync_WhenMeterReadingIsZero_ReturnsReadableZeroResult()
        {
            var jsonFromAi = "{\"status\": \"readable\", \"suggested_value\": 0, \"confidence\": 0.9}";
            var fakeResponse = CreateGeminiResponseJson(jsonFromAi);

            var handler = new FakeHttpMessageHandler
            {
                Handler = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(fakeResponse, Encoding.UTF8, "application/json")
                })
            };
            var service = CreateService(handler);

            var result = await service.ProcessImageAsync(new byte[] { 1, 2, 3 }, "image/png", LoaiDongHo.Nuoc);

            Assert.Equal(MeterOcrStatus.Readable, result.Status);
            Assert.Equal(0m, result.SuggestedValue);
            Assert.Equal(0.9, result.Confidence);
        }

        [Fact]
        public async Task ProcessImageAsync_WhenImageUnreadable_ReturnsUnreadableResult()
        {
            var jsonFromAi = "{\"status\": \"unreadable\", \"reason\": \"Ảnh bị mờ, không thấy mặt đồng hồ\"}";
            var fakeResponse = CreateGeminiResponseJson(jsonFromAi);

            var handler = new FakeHttpMessageHandler
            {
                Handler = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(fakeResponse, Encoding.UTF8, "application/json")
                })
            };
            var service = CreateService(handler);

            var result = await service.ProcessImageAsync(new byte[] { 1, 2, 3 }, "image/jpeg", LoaiDongHo.Dien);

            Assert.Equal(MeterOcrStatus.Unreadable, result.Status);
            Assert.Null(result.SuggestedValue);
            Assert.Contains("mờ", result.ErrorMessage);
        }

        [Fact]
        public async Task ProcessImageAsync_WhenGeminiReturnsNegativeValue_ReturnsFailedResult()
        {
            var jsonFromAi = "{\"status\": \"readable\", \"suggested_value\": -5.2, \"confidence\": 0.8}";
            var fakeResponse = CreateGeminiResponseJson(jsonFromAi);

            var handler = new FakeHttpMessageHandler
            {
                Handler = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(fakeResponse, Encoding.UTF8, "application/json")
                })
            };
            var service = CreateService(handler);

            var result = await service.ProcessImageAsync(new byte[] { 1, 2, 3 }, "image/jpeg", LoaiDongHo.Dien);

            Assert.Equal(MeterOcrStatus.Failed, result.Status);
        }

        [Fact]
        public async Task ProcessImageAsync_WhenGeminiReturnsInvalidConfidence_ReturnsFailedResult()
        {
            var jsonFromAi = "{\"status\": \"readable\", \"suggested_value\": 100, \"confidence\": 1.5}";
            var fakeResponse = CreateGeminiResponseJson(jsonFromAi);

            var handler = new FakeHttpMessageHandler
            {
                Handler = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(fakeResponse, Encoding.UTF8, "application/json")
                })
            };
            var service = CreateService(handler);

            var result = await service.ProcessImageAsync(new byte[] { 1, 2, 3 }, "image/jpeg", LoaiDongHo.Dien);

            Assert.Equal(MeterOcrStatus.Failed, result.Status);
        }

        [Fact]
        public async Task ProcessImageAsync_WhenResponseIsEmptyOrInvalidJson_ReturnsFailedResult()
        {
            var fakeResponse = CreateGeminiResponseJson("This is not a JSON object");

            var handler = new FakeHttpMessageHandler
            {
                Handler = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(fakeResponse, Encoding.UTF8, "application/json")
                })
            };
            var service = CreateService(handler);

            var result = await service.ProcessImageAsync(new byte[] { 1, 2, 3 }, "image/jpeg", LoaiDongHo.Dien);

            Assert.Equal(MeterOcrStatus.Failed, result.Status);
        }

        [Fact]
        public async Task ProcessImageAsync_WhenHttpNonSuccess_ReturnsFailedResult()
        {
            var handler = new FakeHttpMessageHandler
            {
                Handler = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)
                {
                    Content = new StringContent("Server error")
                })
            };
            var service = CreateService(handler);

            var result = await service.ProcessImageAsync(new byte[] { 1, 2, 3 }, "image/jpeg", LoaiDongHo.Dien);

            Assert.Equal(MeterOcrStatus.Failed, result.Status);
        }

        [Fact]
        public async Task ProcessImageAsync_WhenTimeout_ReturnsFailedResult()
        {
            var handler = new FakeHttpMessageHandler
            {
                Handler = (_, _) => throw new TaskCanceledException("Request timeout")
            };
            var service = CreateService(handler);

            var result = await service.ProcessImageAsync(new byte[] { 1, 2, 3 }, "image/jpeg", LoaiDongHo.Dien);

            Assert.Equal(MeterOcrStatus.Failed, result.Status);
        }

        [Fact]
        public async Task ProcessImageAsync_WhenCallerCanceled_ThrowsOperationCanceledException()
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
            var service = CreateService(handler);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            {
                await service.ProcessImageAsync(new byte[] { 1, 2, 3 }, "image/jpeg", LoaiDongHo.Dien, cts.Token);
            });
        }

        [Fact]
        public async Task ProcessImageAsync_SendsCorrectPayloadWithBase64AndContentTypeAndPrompt()
        {
            var jsonFromAi = "{\"status\": \"readable\", \"suggested_value\": 10, \"confidence\": 0.9}";
            var fakeResponse = CreateGeminiResponseJson(jsonFromAi);

            var handler = new FakeHttpMessageHandler
            {
                Handler = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(fakeResponse, Encoding.UTF8, "application/json")
                })
            };
            var service = CreateService(handler);

            byte[] sampleBytes = new byte[] { 10, 20, 30 };
            await service.ProcessImageAsync(sampleBytes, "image/png", LoaiDongHo.Nuoc);

            Assert.NotNull(handler.LastRequestBody);
            Assert.Contains(Convert.ToBase64String(sampleBytes), handler.LastRequestBody);
            Assert.Contains("image/png", handler.LastRequestBody);
            Assert.Contains("đồng hồ nước", handler.LastRequestBody.ToLowerInvariant());
        }

        [Fact]
        public async Task ProcessImageAsync_WhenApiKeyIsEmpty_ReturnsFailedResultWithoutNetworkCall()
        {
            var callCount = 0;
            var handler = new FakeHttpMessageHandler
            {
                Handler = (_, _) =>
                {
                    callCount++;
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
                }
            };
            var httpClient = new HttpClient(handler);
            var inMemorySettings = new System.Collections.Generic.Dictionary<string, string?>
            {
                {"Gemini:ApiKey", ""},
                {"Gemini:Model", "gemini-1.5-flash"}
            };
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(inMemorySettings).Build();
            var service = new GeminiMeterOcrService(httpClient, configuration);

            var result = await service.ProcessImageAsync(new byte[] { 1, 2, 3 }, "image/jpeg", LoaiDongHo.Dien);

            Assert.Equal(MeterOcrStatus.Failed, result.Status);
            Assert.Equal("Dịch vụ nhận diện chưa được cấu hình.", result.ErrorMessage);
            Assert.Equal(0, callCount);
        }

        [Fact]
        public async Task ProcessImageAsync_UsesHeaderForApiKey_AndUrlDoesNotContainApiKey()
        {
            var jsonFromAi = "{\"status\": \"readable\", \"suggested_value\": 10, \"confidence\": 0.9}";
            var fakeResponse = CreateGeminiResponseJson(jsonFromAi);

            var handler = new FakeHttpMessageHandler
            {
                Handler = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(fakeResponse, Encoding.UTF8, "application/json")
                })
            };
            var service = CreateService(handler);

            await service.ProcessImageAsync(new byte[] { 1, 2, 3 }, "image/jpeg", LoaiDongHo.Dien);

            Assert.NotNull(handler.LastRequest);
            // URL không được chứa query string ?key=
            Assert.DoesNotContain("?key=", handler.LastRequest.RequestUri?.ToString() ?? string.Empty);
            Assert.DoesNotContain("fake_test_key", handler.LastRequest.RequestUri?.ToString() ?? string.Empty);
            // Header phải chứa x-goog-api-key
            Assert.True(handler.LastRequest.Headers.Contains("x-goog-api-key"));
            Assert.Equal("fake_test_key", handler.LastRequest.Headers.GetValues("x-goog-api-key").First());
        }

        [Fact]
        public async Task ProcessImageAsync_WhenReasonIsLongerThan500Characters_TruncatesReasonTo500Characters()
        {
            var longReason = new string('A', 600);
            var jsonFromAi = $"{{\"status\": \"unreadable\", \"reason\": \"{longReason}\"}}";
            var fakeResponse = CreateGeminiResponseJson(jsonFromAi);

            var handler = new FakeHttpMessageHandler
            {
                Handler = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(fakeResponse, Encoding.UTF8, "application/json")
                })
            };
            var service = CreateService(handler);

            var result = await service.ProcessImageAsync(new byte[] { 1, 2, 3 }, "image/jpeg", LoaiDongHo.Dien);

            Assert.Equal(MeterOcrStatus.Unreadable, result.Status);
            Assert.NotNull(result.ErrorMessage);
            Assert.Equal(500, result.ErrorMessage.Length);
            Assert.Equal(new string('A', 500), result.ErrorMessage);
        }
    }
}
