using System;
using System.Threading.Tasks;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Services.AiAssistants;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels.AiAssistant;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Controllers
{
    public class AiAssistantController : AdminBaseController
    {
        private readonly IAiAssistantService _aiService;
        private readonly ILogger<AiAssistantController> _logger;

        public AiAssistantController(IAiAssistantService aiService, ILogger<AiAssistantController> logger)
        {
            _aiService = aiService;
            _logger = logger;
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
                string userRole = User.IsInRole("Admin") ? "Admin" : "NhanVien";
                int? nguoiThueId = null;

                var result = await _aiService.ChatWithAssistantAsync(request.Message, request.History, userRole, nguoiThueId);
                return Json(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi xảy ra trong quá trình chat với trợ lý ảo.");
                return Json(QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels.ServiceResult.Fail("Đã xảy ra lỗi hệ thống khi xử lý yêu cầu."));
            }
        }
    }
}
