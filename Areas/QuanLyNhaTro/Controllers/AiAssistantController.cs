using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Services.AiAssistants;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels.AiAssistant;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Controllers
{
    [Area("QuanLyNhaTro")]
    [Authorize]
    public class AiAssistantController : Controller
    {
        private readonly IAiAssistantService _aiService;

        public AiAssistantController(IAiAssistantService aiService)
        {
            _aiService = aiService;
        }

        [HttpPost]
        public async Task<IActionResult> Chat([FromBody] ChatRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Message))
            {
                return Json(new { success = false, message = "Nội dung tin nhắn không được trống." });
            }

            try
            {
                var reply = await _aiService.ChatWithAssistantAsync(request.Message, request.History);
                return Json(new { success = true, reply = reply });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Đã xảy ra lỗi hệ thống: {ex.Message}" });
            }
        }
    }
}
