using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Features.AiAssistants.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.AiAssistants.Services;

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
        public async Task<IActionResult> Chat([FromBody] ChatRequest? request, CancellationToken ct)
        {
            try
            {
                var result = await _aiService.ChatQuanLyAsync(CurrentActorId, request ?? new ChatRequest(), ct);
                return Json(result);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi xảy ra trong quá trình chat với trợ lý ảo.");
                return Json(ServiceResult.Fail("Đã xảy ra lỗi hệ thống khi xử lý yêu cầu."));
            }
        }
    }
}
