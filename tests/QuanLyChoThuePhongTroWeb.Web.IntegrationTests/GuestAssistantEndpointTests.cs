using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using QuanLyChoThuePhongTroWeb.Application.Features.AiAssistants.ChatModel;
using QuanLyChoThuePhongTroWeb.Web.IntegrationTests.Fixtures;

namespace QuanLyChoThuePhongTroWeb.Web.IntegrationTests;

[Collection(WebTestCollection.Name)]
public sealed class GuestAssistantEndpointTests(CustomWebApplicationFactory factory)
{
    private const string Endpoint = "/phong/tro-ly-ai";
    private WebApplicationFactory<Program> App()
    {
        factory.FakeAiChatModelInstance.Reset();
        return factory.WithWebHostBuilder(_ => { });
    }

    [Fact]
    public async Task Anonymous_CanChat_WithOnlyPublicTools()
    {
        using var app = App();
        using var client = app.CreateClient();
        var token = await TokenAsync(client);
        var response = await SendAsync(client, token, "Tìm phòng");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(json.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal(new[] { "TimPhongCongKhai", "XemPhongCongKhai" }, factory.FakeAiChatModelInstance.Requests.Single().Tools.Select(t => t.Name));
    }

    [Fact]
    public async Task MissingCsrfToken_IsRejectedBeforeCallingModel()
    {
        using var app = App();
        using var client = app.CreateClient();
        var response = await client.PostAsJsonAsync(Endpoint, new { message = "Xin chào" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(factory.FakeAiChatModelInstance.Requests);
    }

    [Fact]
    public async Task EmptyAndLongMessages_AreBadRequests()
    {
        using var app = App();
        using var client = app.CreateClient();
        var token = await TokenAsync(client);
        Assert.Equal(HttpStatusCode.BadRequest, (await SendAsync(client, token, " ")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await SendAsync(client, token, new('x', 1001))).StatusCode);
        Assert.Empty(factory.FakeAiChatModelInstance.Requests);
    }

    [Fact]
    public async Task RateLimit_RejectsSeventhRequestWithoutCallingModel()
    {
        using var app = App();
        using var client = app.CreateClient();
        var token = await TokenAsync(client);
        for (var n = 0; n < 6; n++)
            Assert.Equal(HttpStatusCode.OK, (await SendAsync(client, token, "Xin chào")).StatusCode);
        var rejected = await SendAsync(client, token, "Xin chào");
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.Contains("thử lại", await rejected.Content.ReadAsStringAsync());
        Assert.Equal(6, factory.FakeAiChatModelInstance.Requests.Count);
    }

    [Fact]
    public async Task ModelUnavailable_ReturnsRecoverableError()
    {
        using var app = App();
        factory.FakeAiChatModelInstance.Responder = (_, _) => AiModelResult.Fail(AiModelErrorKind.NotConfigured);
        using var client = app.CreateClient();
        var response = await SendAsync(client, await TokenAsync(client), "Tìm phòng");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.DoesNotContain("API", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task PublicWidget_HasOwnEndpointAndIsNotManagerWidget()
    {
        using var app = App();
        using var client = app.CreateClient();
        var html = await client.GetStringAsync("/phong");
        Assert.Contains("data-guest-ai", html);
        Assert.Contains("/phong/tro-ly-ai", html);
        Assert.DoesNotContain("/QuanLyNhaTro/AiAssistant/Chat", html);
        Assert.DoesNotContain("ai_chat_history_", html);
    }

    private static async Task<string> TokenAsync(HttpClient client)
    {
        var html = await client.GetStringAsync("/phong");
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(token.Success, "Public assistant must render its antiforgery token.");
        return WebUtility.HtmlDecode(token.Groups[1].Value);
    }
    private static Task<HttpResponseMessage> SendAsync(HttpClient client, string token, string message)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, Endpoint) { Content = JsonContent.Create(new { message }) };
        request.Headers.Add("RequestVerificationToken", token);
        return client.SendAsync(request);
    }
}
