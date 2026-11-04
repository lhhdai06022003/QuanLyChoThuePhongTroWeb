using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuanLyChoThuePhongTroWeb.Web.Helpers;
using QuanLyChoThuePhongTroWeb.Application.Features.GuestAssistant;

namespace QuanLyChoThuePhongTroWeb.Controllers;

[AllowAnonymous]
[Route("phong/tro-ly-ai")]
public sealed class GuestAssistantController(IGuestAssistantService assistant,
    IConfiguration configuration, ILogger<GuestAssistantController> logger) : Controller
{
    [HttpPost]
    [ValidateAntiForgeryToken]
    [GuestAssistantRateLimit]
    [RequestSizeLimit(32768)]
    public async Task<IActionResult> Chat([FromBody] GuestAssistantRequest? request, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(GuestAssistantReply.Fail("Câu hỏi chưa hợp lệ. Bạn hãy thử nhập lại.", "INVALID_MESSAGE"));
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(40));
        try
        {
            var allowed = configuration.GetSection("PublicRooms:RoomIds").Get<int[]>();
            var reply = await assistant.ChatAsync(request, allowed, timeout.Token);
            return reply.Success ? Ok(reply) : StatusCode(reply.ErrorCode == "INVALID_MESSAGE" ? 400 : 503, reply);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return Unavailable();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Không hoàn tất tra cứu AI công khai");
            return Unavailable();
        }
    }

    private ObjectResult Unavailable() => StatusCode(503, GuestAssistantReply.Fail(
        "Trợ lý AI đang bận. Bạn hãy thử lại hoặc dùng bộ lọc tìm phòng trên trang.", "AI_UNAVAILABLE"));
}
