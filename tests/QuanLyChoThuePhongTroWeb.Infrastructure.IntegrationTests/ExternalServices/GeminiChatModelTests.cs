using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using QuanLyChoThuePhongTroWeb.Application.Features.AiAssistants.ChatModel;
using QuanLyChoThuePhongTroWeb.Infrastructure.ExternalServices.AiAssistants;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.ExternalServices
{
    // Không dùng PostgreSqlFixture và không gọi Gemini thật: mọi request đi qua handler giả.
    public class GeminiChatModelTests
    {
        private class FakeHttpMessageHandler : HttpMessageHandler
        {
            public Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> Handler { get; set; } = null!;
            public List<HttpRequestMessage> Requests { get; } = new();
            public string? LastBody { get; private set; }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Requests.Add(request);
                if (request.Content != null)
                {
                    LastBody = await request.Content.ReadAsStringAsync(cancellationToken);
                }
                return await Handler(request, cancellationToken);
            }
        }

        private class CapturingLogger<T> : ILogger<T>
        {
            public List<(LogLevel Level, string Message)> Entries { get; } = new();
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
                => Entries.Add((logLevel, formatter(state, exception)));
        }

        private static GeminiChatModel CreateModel(FakeHttpMessageHandler handler, string apiKey = "fake_test_key", ILogger<GeminiChatModel>? logger = null)
        {
            var settings = new Dictionary<string, string?> { { "Gemini:ApiKey", apiKey }, { "Gemini:Model", "gemini-test" } };
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
            return new GeminiChatModel(new HttpClient(handler), configuration, logger ?? NullLogger<GeminiChatModel>.Instance);
        }

        private static FakeHttpMessageHandler Respond(HttpStatusCode status, string body)
            => new()
            {
                Handler = (_, _) => Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") })
            };

        private const string TextResponse = "{\"candidates\":[{\"content\":{\"role\":\"model\",\"parts\":[{\"text\":\"Xin chào\"}]}}]}";

        private static AiModelRequest SimpleRequest(bool allowTools = true, params AiTurn[] extraTurns)
        {
            var turns = new List<AiTurn> { new(AiTurnRole.User, new AiPart[] { new AiTextPart("hỏi") }) };
            turns.AddRange(extraTurns);
            var tools = new List<AiToolDefinition>
            {
                new("GetPhongTrongAsync", "Phòng trống", new[] { new AiToolParameter("mucGiaToiDa", AiToolParameterType.Number, "giá") }),
                new("GetThongTinKhachThueAsync", "Tra khách", new[] { new AiToolParameter("tuKhoa", AiToolParameterType.String, "từ khóa", true) }),
                new("GetMyHopDongInfoAsync", "Hợp đồng", Array.Empty<AiToolParameter>())
            };
            return new AiModelRequest("hệ thống", turns, tools, allowTools);
        }

        [Fact]
        public async Task ApiKey_IsSentInHeader_NotInUrl()
        {
            var handler = Respond(HttpStatusCode.OK, TextResponse);

            var result = await CreateModel(handler).GenerateAsync(SimpleRequest());

            Assert.True(result.Success);
            Assert.Equal("Xin chào", result.Text);
            var request = Assert.Single(handler.Requests);
            Assert.Equal("fake_test_key", Assert.Single(request.Headers.GetValues("x-goog-api-key")));
            Assert.DoesNotContain("key=", request.RequestUri!.Query);
            Assert.DoesNotContain("fake_test_key", request.RequestUri.ToString());
            Assert.Contains("gemini-test:generateContent", request.RequestUri.ToString());
        }

        [Fact]
        public async Task HttpError_ReturnsHttpError_WithoutLeakingBody()
        {
            var handler = Respond(HttpStatusCode.BadRequest, "{\"error\":\"BI_MAT_API_KEY_123\"}");
            var logger = new CapturingLogger<GeminiChatModel>();

            var result = await CreateModel(handler, logger: logger).GenerateAsync(SimpleRequest());

            Assert.False(result.Success);
            Assert.Equal(AiModelErrorKind.HttpError, result.ErrorKind);
            Assert.Equal(400, result.HttpStatusCode);
            Assert.Null(result.Text);
            Assert.DoesNotContain("BI_MAT_API_KEY_123", result.ToString());
            Assert.DoesNotContain(logger.Entries, e => e.Level == LogLevel.Error);
            Assert.Contains(logger.Entries, e => e.Level == LogLevel.Warning);
        }

        [Fact]
        public async Task MissingApiKey_ReturnsNotConfigured_AndSendsNothing()
        {
            var handler = Respond(HttpStatusCode.OK, TextResponse);

            var result = await CreateModel(handler, apiKey: "").GenerateAsync(SimpleRequest());

            Assert.False(result.Success);
            Assert.Equal(AiModelErrorKind.NotConfigured, result.ErrorKind);
            Assert.Empty(handler.Requests);
        }

        [Fact]
        public async Task Body_ContainsDeclarationsRequiredAndSystemInstruction()
        {
            var handler = Respond(HttpStatusCode.OK, TextResponse);

            await CreateModel(handler).GenerateAsync(SimpleRequest());

            var body = JsonNode.Parse(handler.LastBody!)!;
            Assert.Equal("hệ thống", body["systemInstruction"]!["parts"]![0]!["text"]!.ToString());
            Assert.Equal("user", body["contents"]![0]!["role"]!.ToString());

            var declarations = body["tools"]![0]!["functionDeclarations"]!.AsArray();
            Assert.Equal(3, declarations.Count);
            Assert.Equal("NUMBER", declarations[0]!["parameters"]!["properties"]!["mucGiaToiDa"]!["type"]!.ToString());
            Assert.Null(declarations[0]!["parameters"]!["required"]);
            Assert.Equal("STRING", declarations[1]!["parameters"]!["properties"]!["tuKhoa"]!["type"]!.ToString());
            Assert.Equal("tuKhoa", declarations[1]!["parameters"]!["required"]![0]!.ToString());
            Assert.Null(declarations[2]!["parameters"]);
            Assert.Null(body["toolConfig"]);
        }

        [Fact]
        public async Task Body_WithToolCallsDisabled_SetsFunctionCallingModeNone()
        {
            var handler = Respond(HttpStatusCode.OK, TextResponse);

            await CreateModel(handler).GenerateAsync(SimpleRequest(allowTools: false));

            var body = JsonNode.Parse(handler.LastBody!)!;
            Assert.Equal("NONE", body["toolConfig"]!["functionCallingConfig"]!["mode"]!.ToString());
        }

        [Fact]
        public async Task Body_ModelTurnRawContentIsSentVerbatim_AndFunctionResponsesCarryNameIdResponse()
        {
            var handler = Respond(HttpStatusCode.OK, TextResponse);
            const string raw = "{\"role\":\"model\",\"parts\":[{\"functionCall\":{\"name\":\"GetPhongTrongAsync\",\"args\":{}},\"thoughtSignature\":\"SIG123\"}]}";
            var modelTurn = new AiTurn(AiTurnRole.Model,
                new AiPart[] { new AiFunctionCallPart(new AiFunctionCall("GetPhongTrongAsync", "{}", "c1")) }, raw);
            var responseTurn = new AiTurn(AiTurnRole.User, new AiPart[]
            {
                new AiFunctionResponsePart("GetPhongTrongAsync", "c1", "{\"thanhCong\":true,\"soLuong\":0}"),
                new AiFunctionResponsePart("GetMyHopDongInfoAsync", null, "{\"thanhCong\":true}")
            });

            await CreateModel(handler).GenerateAsync(SimpleRequest(true, modelTurn, responseTurn));

            var contents = JsonNode.Parse(handler.LastBody!)!["contents"]!.AsArray();
            Assert.Equal(3, contents.Count);
            Assert.Equal("SIG123", contents[1]!["parts"]![0]!["thoughtSignature"]!.ToString());
            Assert.Equal("model", contents[1]!["role"]!.ToString());

            var parts = contents[2]!["parts"]!.AsArray();
            Assert.Equal("user", contents[2]!["role"]!.ToString());
            Assert.Equal(2, parts.Count);
            Assert.Equal("GetPhongTrongAsync", parts[0]!["functionResponse"]!["name"]!.ToString());
            Assert.Equal("c1", parts[0]!["functionResponse"]!["id"]!.ToString());
            Assert.Equal(0, (int)parts[0]!["functionResponse"]!["response"]!["soLuong"]!);
            Assert.Null(parts[1]!["functionResponse"]!["id"]);
        }

        [Fact]
        public async Task Response_TwoFunctionCallsAndThoughtText_KeepsCallsInOrder_IgnoresThought()
        {
            const string response = "{\"candidates\":[{\"content\":{\"role\":\"model\",\"parts\":[" +
                "{\"text\":\"suy nghi noi bo\",\"thought\":true}," +
                "{\"functionCall\":{\"name\":\"GetPhongTrongAsync\",\"args\":{\"mucGiaToiDa\":2000000},\"id\":\"a\"}}," +
                "{\"functionCall\":{\"name\":\"GetMyHopDongInfoAsync\"}}]}}]}";
            var handler = Respond(HttpStatusCode.OK, response);

            var result = await CreateModel(handler).GenerateAsync(SimpleRequest());

            Assert.True(result.Success);
            Assert.Equal(2, result.FunctionCalls.Count);
            Assert.Equal("GetPhongTrongAsync", result.FunctionCalls[0].Name);
            Assert.Equal("a", result.FunctionCalls[0].Id);
            Assert.Equal(2000000, (int)JsonNode.Parse(result.FunctionCalls[0].ArgumentsJson)!["mucGiaToiDa"]!);
            Assert.Equal("GetMyHopDongInfoAsync", result.FunctionCalls[1].Name);
            Assert.Null(result.FunctionCalls[1].Id);
            Assert.Equal("{}", result.FunctionCalls[1].ArgumentsJson);
            Assert.Null(result.Text);
            Assert.NotNull(result.ModelTurn);
            Assert.Contains("functionCall", result.ModelTurn!.RawProviderContent);
        }

        [Fact]
        public async Task Response_TextPartsAreConcatenated_ThoughtExcluded()
        {
            const string response = "{\"candidates\":[{\"content\":{\"parts\":[" +
                "{\"text\":\"bo qua\",\"thought\":true},{\"text\":\"Xin \"},{\"text\":\"chào\"}]}}]}";
            var handler = Respond(HttpStatusCode.OK, response);

            var result = await CreateModel(handler).GenerateAsync(SimpleRequest());

            Assert.Equal("Xin chào", result.Text);
            Assert.Empty(result.FunctionCalls);
        }

        [Theory]
        [InlineData("{\"candidates\":[]}")]
        [InlineData("{\"promptFeedback\":{\"blockReason\":\"SAFETY\"}}")]
        [InlineData("{\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"x\",\"thought\":true}]}}]}")]
        public async Task Response_WithoutUsableContent_IsEmptyResponse(string body)
        {
            var result = await CreateModel(Respond(HttpStatusCode.OK, body)).GenerateAsync(SimpleRequest());

            Assert.False(result.Success);
            Assert.Equal(AiModelErrorKind.EmptyResponse, result.ErrorKind);
        }

        [Fact]
        public async Task NetworkFailure_ReturnsUnavailable_WithoutThrowing()
        {
            var handler = new FakeHttpMessageHandler { Handler = (_, _) => throw new HttpRequestException("boom") };
            var logger = new CapturingLogger<GeminiChatModel>();

            var result = await CreateModel(handler, logger: logger).GenerateAsync(SimpleRequest());

            Assert.False(result.Success);
            Assert.Equal(AiModelErrorKind.Unavailable, result.ErrorKind);
            Assert.Contains(logger.Entries, e => e.Level == LogLevel.Warning);
            Assert.DoesNotContain(logger.Entries, e => e.Level == LogLevel.Error);
        }

        [Fact]
        public async Task InvalidJsonResponse_ReturnsUnavailable()
        {
            var result = await CreateModel(Respond(HttpStatusCode.OK, "not json")).GenerateAsync(SimpleRequest());

            Assert.Equal(AiModelErrorKind.Unavailable, result.ErrorKind);
        }

        [Fact]
        public async Task CallerCancellation_IsPropagated()
        {
            var handler = new FakeHttpMessageHandler { Handler = (_, ct) => Task.FromCanceled<HttpResponseMessage>(ct) };
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CreateModel(handler).GenerateAsync(SimpleRequest(), cts.Token));
        }
    }
}
