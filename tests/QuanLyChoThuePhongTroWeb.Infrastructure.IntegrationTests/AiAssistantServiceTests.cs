using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;
using QuanLyChoThuePhongTroWeb.Infrastructure.ExternalServices.AiAssistants;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests;

public class AiAssistantServiceTests
{
    [Fact]
    public async Task ChatWithAssistantAsync_PreservesThoughtSignatureWhenReturningToolResult()
    {
        var callCount = 0;
        using var client = new HttpClient(new CallbackHandler(async request =>
        {
            callCount++;
            if (callCount == 1)
            {
                return JsonResponse(HttpStatusCode.OK,
                    """{"candidates":[{"content":{"role":"model","parts":[{"functionCall":{"name":"UnknownTool","args":{}},"thoughtSignature":"opaque-signature"}]}}]}""");
            }

            var body = JsonNode.Parse(await request.Content!.ReadAsStringAsync())!;
            var contents = body["contents"]!.AsArray();
            var modelContent = contents[^2];
            var toolContent = contents[^1];
            var signaturePreserved = modelContent?["role"]?.ToString() == "model"
                && modelContent["parts"]?[0]?["thoughtSignature"]?.ToString() == "opaque-signature"
                && modelContent["parts"]?[0]?["functionCall"]?["name"]?.ToString() == "UnknownTool"
                && toolContent?["role"]?.ToString() == "user"
                && toolContent["parts"]?[0]?["functionResponse"]?["name"]?.ToString() == "UnknownTool";

            return signaturePreserved
                ? JsonResponse(HttpStatusCode.OK, """{"candidates":[{"content":{"role":"model","parts":[{"text":"Đã xử lý"}]}}]}""")
                : JsonResponse(HttpStatusCode.BadRequest, """{"error":"Invalid function history"}""");
        }));

        var result = await CreateService(client).ChatWithAssistantAsync("Doanh thu tháng 8", [], "Admin");

        Assert.True(result.Success, result.Message);
        Assert.Equal("Đã xử lý", result.Message);
        Assert.Equal(2, callCount);
    }

    [Fact]
    public async Task ChatWithAssistantAsync_SendsApiKeyInHeaderWithoutExposingItInUrl()
    {
        using var client = new HttpClient(new CallbackHandler(request =>
        {
            var hasKeyHeader = request.Headers.TryGetValues("x-goog-api-key", out var values)
                && values.SingleOrDefault() == "test-api-key";
            var hasNoKeyInUrl = string.IsNullOrEmpty(request.RequestUri?.Query);
            return Task.FromResult(hasKeyHeader && hasNoKeyInUrl
                ? JsonResponse(HttpStatusCode.OK, """{"candidates":[{"content":{"role":"model","parts":[{"text":"Xin chào"}]}}]}""")
                : JsonResponse(HttpStatusCode.BadRequest, """{"error":"API key must be sent in a header"}"""));
        }));

        var result = await CreateService(client).ChatWithAssistantAsync("Xin chào", [], "Admin");

        Assert.True(result.Success, result.Message);
        Assert.Equal("Xin chào", result.Message);
    }

    private static AiAssistantService CreateService(HttpClient client)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Gemini:ApiKey"] = "test-api-key",
                ["Gemini:Model"] = "gemini-3.6-flash"
            })
            .Build();

        return new AiAssistantService(null!, client, configuration);
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, string json) =>
        new(statusCode) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private sealed class CallbackHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> callback) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            callback(request);
    }
}
