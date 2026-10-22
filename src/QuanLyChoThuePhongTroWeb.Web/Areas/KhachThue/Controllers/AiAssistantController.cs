using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Features.AiAssistants.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.AiAssistants.Services;

namespace QuanLyChoThuePhongTroWeb.Areas.KhachThue.Controllers
{
    // Trợ lý AI của khách thuê: id người thuê luôn lấy từ claim, không nhận từ client.
    [Area("KhachThue")]
    [Authorize(Roles = "KhachThue")]
    [AutoValidateAntiforgeryToken]
    public class AiAssistantController : Controller
    {
        private readonly IAiAssistantService _aiService;
        private readonly ILogger<AiAssistantController> _logger;

        public AiAssistantController(IAiAssistantService aiService, ILogger<AiAssistantController> logger)
        {
            _aiService = aiService;
            _logger = logger;
        }

        [HttpPost]
        public async Task<IActionResult> Chat([FromBody] ChatRequest? request, CancellationToken ct)
        {
            if (!TryGetTenant(out var nguoiThueId, out var userId))
            {
                return Unauthorized(new { success = false, message = "Tài khoản chưa liên kết hồ sơ người thuê." });
            }

            try
            {
                var result = await _aiService.ChatKhachThueAsync(userId, nguoiThueId, request ?? new ChatRequest(), ct);
                return Json(result);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi xảy ra trong quá trình chat với trợ lý ảo của khách thuê.");
                return Json(ServiceResult.Fail("Đã xảy ra lỗi hệ thống khi xử lý yêu cầu."));
            }
        }

        private bool TryGetTenant(out int nguoiThueId, out int userId)
        {
            userId = 0;
            return int.TryParse(User.FindFirst("NguoiThueId")?.Value, out nguoiThueId) && nguoiThueId > 0 &&
                   int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId) && userId > 0;
        }
    }
}
