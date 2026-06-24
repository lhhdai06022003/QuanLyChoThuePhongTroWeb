using System;
using System.Threading.Tasks;
using System.Security.Claims;
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
                return Json(QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels.ServiceResult.Fail("Nội dung tin nhắn không được trống."));
            }

            try
            {
                string userRole = User.IsInRole("Admin") ? "Admin" : User.IsInRole("KhachThue") ? "KhachThue" : "Unknown";
                int? nguoiThueId = null;

                if (userRole == "KhachThue")
                {
                    var claim = User.FindFirst("NguoiThueId");
                    if (claim != null && int.TryParse(claim.Value, out int parsedId))
                    {
                        nguoiThueId = parsedId;
                    }
                }

                var result = await _aiService.ChatWithAssistantAsync(request.Message, request.History, userRole, nguoiThueId);
                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels.ServiceResult.Fail($"Đã xảy ra lỗi hệ thống: {ex.Message}"));
            }
        }
    }
}
