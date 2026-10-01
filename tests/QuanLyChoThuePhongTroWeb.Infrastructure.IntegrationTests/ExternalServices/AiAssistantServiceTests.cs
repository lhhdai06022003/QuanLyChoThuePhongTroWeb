using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using QuanLyChoThuePhongTroWeb.Application.Features.AiAssistants.DTOs;
using QuanLyChoThuePhongTroWeb.Infrastructure.ExternalServices.AiAssistants;
using QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.Fixtures;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.ExternalServices
{
    public class AiAssistantServiceTests : IClassFixture<PostgreSqlFixture>
    {
        private readonly PostgreSqlFixture _fixture;

        public AiAssistantServiceTests(PostgreSqlFixture fixture)
        {
            _fixture = fixture;
        }

        private class FakeHttpMessageHandler : HttpMessageHandler
        {
            public List<HttpRequestMessage> Requests { get; } = new();
            public Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> Handler { get; set; } = null!;

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Requests.Add(request);
                return await Handler(request, cancellationToken);
            }
        }

        private AiAssistantService CreateService(FakeHttpMessageHandler handler, string apiKey = "fake_gemini_api_key_123")
        {
            var db = _fixture.CreateDbContext();
            var httpClient = new HttpClient(handler);
            var inMemorySettings = new Dictionary<string, string?>
            {
                {"Gemini:ApiKey", apiKey},
                {"Gemini:Model", "gemini-1.5-flash"}
            };
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(inMemorySettings)
                .Build();

            return new AiAssistantService(db, httpClient, config);
        }

        private static string CreateSimpleTextResponse(string text)
        {
            return JsonSerializer.Serialize(new
            {
                candidates = new[]
                {
                    new
                    {
                        content = new
                        {
                            parts = new[] { new { text = text } }
                        }
                    }
                }
            });
        }

        [Fact]
        public async Task ChatWithAssistantAsync_RequestUriDoesNotContainKey_AndHeaderContainsApiKey()
        {
            var fakeHandler = new FakeHttpMessageHandler();
            fakeHandler.Handler = (req, ct) =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(CreateSimpleTextResponse("Xin chào!"), System.Text.Encoding.UTF8, "application/json")
                };
                return Task.FromResult(response);
            };

            var service = CreateService(fakeHandler, "secret_gemini_key_456");

            var result = await service.ChatWithAssistantAsync("Chào bạn", new List<ChatMessageDto>(), "KhachThue", null);

            Assert.True(result.Success);
            Assert.Single(fakeHandler.Requests);
            var sentRequest = fakeHandler.Requests[0];
            Assert.DoesNotContain("key=", sentRequest.RequestUri?.Query ?? string.Empty);
            Assert.True(sentRequest.Headers.Contains("x-goog-api-key"));
            Assert.Equal("secret_gemini_key_456", sentRequest.Headers.GetValues("x-goog-api-key").First());
        }

        [Fact]
        public async Task ChatWithAssistantAsync_WhenApiReturnsError_DoesNotLeakRawErrorContent()
        {
            var fakeHandler = new FakeHttpMessageHandler();
            const string rawLeakString = "RAW_SECRET_LEAK_IN_RESPONSE_BODY_XYZ";
            fakeHandler.Handler = (req, ct) =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.BadRequest)
                {
                    Content = new StringContent($"{{\"error\": \"{rawLeakString}\"}}", System.Text.Encoding.UTF8, "application/json")
                };
                return Task.FromResult(response);
            };

            var service = CreateService(fakeHandler, "some_key");

            var result = await service.ChatWithAssistantAsync("Hỏi gì đó", new List<ChatMessageDto>(), "KhachThue", null);

            Assert.False(result.Success);
            Assert.DoesNotContain(rawLeakString, result.Message);
            Assert.Equal("Trợ lý AI tạm thời không phản hồi (HTTP 400). Vui lòng thử lại sau.", result.Message);
        }

        [Fact]
        public async Task ChatWithAssistantAsync_WhenApiKeyIsEmpty_ReturnsConfigErrorAndDoesNotSendRequest()
        {
            var fakeHandler = new FakeHttpMessageHandler();
            var service = CreateService(fakeHandler, apiKey: "");

            var result = await service.ChatWithAssistantAsync("Hỏi gì đó", new List<ChatMessageDto>(), "KhachThue", null);

            Assert.False(result.Success);
            Assert.Contains("Lỗi cấu hình", result.Message);
            Assert.Empty(fakeHandler.Requests);
        }

        [Fact]
        public async Task ChatWithAssistantAsync_FunctionCall_BothRequestsDoNotContainKey_AndIncludeHeader()
        {
            var fakeHandler = new FakeHttpMessageHandler();
            const string apiKey = "gemini_key_round_1_and_2";
            int callIndex = 0;

            fakeHandler.Handler = (req, ct) =>
            {
                callIndex++;
                if (callIndex == 1)
                {
                    // Lượt 1: AI trả functionCall
                    var functionCallJson = JsonSerializer.Serialize(new
                    {
                        candidates = new[]
                        {
                            new
                            {
                                content = new
                                {
                                    parts = new object[]
                                    {
                                        new
                                        {
                                            functionCall = new
                                            {
                                                name = "GetPhongTroChuaChotDienNuocAsync",
                                                args = new { thang = 9, nam = 2026 }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    });

                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(functionCallJson, System.Text.Encoding.UTF8, "application/json")
                    });
                }
                else
                {
                    // Lượt 2: AI trả câu trả lời cuối
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(CreateSimpleTextResponse("Danh sách phòng chưa chốt."), System.Text.Encoding.UTF8, "application/json")
                    });
                }
            };

            var service = CreateService(fakeHandler, apiKey);

            var result = await service.ChatWithAssistantAsync("Kiểm tra phòng chưa chốt", new List<ChatMessageDto>(), "Admin", null);

            Assert.True(result.Success);
            Assert.Equal(2, fakeHandler.Requests.Count);

            foreach (var req in fakeHandler.Requests)
            {
                Assert.DoesNotContain("key=", req.RequestUri?.Query ?? string.Empty);
                Assert.True(req.Headers.Contains("x-goog-api-key"));
                Assert.Equal(apiKey, req.Headers.GetValues("x-goog-api-key").First());
            }
        }
    }
}
