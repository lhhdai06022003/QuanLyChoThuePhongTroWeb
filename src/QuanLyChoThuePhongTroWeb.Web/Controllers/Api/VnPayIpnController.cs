using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuanLyChoThuePhongTroWeb.Application.Common.Configurations;
using QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;

namespace QuanLyChoThuePhongTroWeb.Controllers.Api;

// IPN VNPay báo kết quả thanh toán (spec VNPay §5.2). Ẩn danh; tin cậy nhờ chữ ký HMAC-SHA512 do service kiểm.
// Không dùng [ApiController] và model binding vì cần toàn bộ tham số query để kiểm chữ ký.
[AllowAnonymous]
[IgnoreAntiforgeryToken]
[Route("api/webhooks/payment/vnpay-ipn")]
public sealed class VnPayIpnController(VnPayOptions options, IVnPayPaymentService vnPay) : ControllerBase
{
    [HttpGet("")]
    public async Task<IActionResult> Receive(CancellationToken cancellationToken)
    {
        // Tắt thì endpoint coi như không tồn tại.
        if (!options.Enabled)
        {
            return NotFound();
        }

        var query = Request.Query.ToDictionary(q => q.Key, q => q.Value.ToString(), StringComparer.Ordinal);
        var result = await vnPay.HandleIpnAsync(query, cancellationToken);

        // VNPay đọc RspCode/Message đúng hoa thường này, luôn HTTP 200. Dùng JsonSerializer mặc định để không bị đổi sang camelCase.
        return Content(JsonSerializer.Serialize(new { result.RspCode, result.Message }), "application/json");
    }
}
