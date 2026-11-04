using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using QuanLyChoThuePhongTroWeb.Application.Features.GuestAssistant;

namespace QuanLyChoThuePhongTroWeb.Web.Helpers
{
    // Gắn lên action gọi trợ lý AI công khai (Gemini) để áp hai lớp giới hạn bên dưới.
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public sealed class GuestAssistantRateLimitAttribute : Attribute;

    // Trợ lý AI cho cả khách chưa đăng nhập nên phải chặn gọi liên tục để giữ quota Gemini.
    // Lớp 1: mỗi tài khoản (hoặc mỗi IP khi chưa đăng nhập) 6 lượt/phút.
    // Lớp 2: cả hệ thống 30 lượt/phút, vẫn giữ quota khi nhiều khách bị thấy chung một IP (Docker, proxy).
    // Lượt bị chặn ở lớp 1 không trừ vào lớp 2, nên một người gửi dồn không làm hết lượt của người khác.
    public static class GuestAssistantRateLimiting
    {
        public const int PermitsPerVisitor = 6;
        public const int PermitsPerMinuteTotal = 30;

        public static IServiceCollection AddGuestAssistantRateLimiting(this IServiceCollection services) =>
            services.AddRateLimiter(options =>
            {
                options.GlobalLimiter = PartitionedRateLimiter.CreateChained(
                    PartitionedRateLimiter.Create<HttpContext, string>(context => IsGuestAssistant(context)
                        ? RateLimitPartition.GetFixedWindowLimiter(PartitionKey(context), _ => PerMinute(PermitsPerVisitor))
                        : RateLimitPartition.GetNoLimiter(string.Empty)),
                    PartitionedRateLimiter.Create<HttpContext, string>(context => IsGuestAssistant(context)
                        ? RateLimitPartition.GetFixedWindowLimiter("total", _ => PerMinute(PermitsPerMinuteTotal))
                        : RateLimitPartition.GetNoLimiter(string.Empty)));
                options.OnRejected = async (context, ct) =>
                {
                    context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                    context.HttpContext.Response.Headers.RetryAfter = "60";
                    await context.HttpContext.Response.WriteAsJsonAsync(GuestAssistantReply.Fail(
                        "Bạn hỏi hơi nhanh. Vui lòng thử lại sau khoảng một phút.", "RATE_LIMITED"), ct);
                };
            });

        // IP lấy từ kết nối trực tiếp; chỉ tin X-Forwarded-For khi đã cấu hình reverse proxy (xem AI_TIM_PHONG.md).
        public static string PartitionKey(HttpContext context) =>
            context.User.Identity?.IsAuthenticated == true &&
            context.User.FindFirstValue(ClaimTypes.NameIdentifier) is { Length: > 0 } userId
                ? "user:" + userId
                : "ip:" + (context.Connection.RemoteIpAddress?.ToString() ?? "unknown");

        private static bool IsGuestAssistant(HttpContext context) =>
            context.GetEndpoint()?.Metadata.GetMetadata<GuestAssistantRateLimitAttribute>() is not null;

        private static FixedWindowRateLimiterOptions PerMinute(int permits) => new()
        {
            PermitLimit = permits,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        };
    }
}
