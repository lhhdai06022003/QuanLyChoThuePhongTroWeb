using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Security;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Controllers
{
    [ApiController]
    public class DienNuocController : AdminBaseController
    {
        private readonly IDienNuocService _dienNuocService;
        private readonly IPhongTroService _phongTroService;
        private readonly ILogger<DienNuocController> _logger;
        private readonly IEmployeeAccessService _employeeAccessService;

        public DienNuocController(IDienNuocService dienNuocService, IPhongTroService phongTroService, ILogger<DienNuocController> logger, IEmployeeAccessService employeeAccessService)
        {
            _dienNuocService = dienNuocService;
            _phongTroService = phongTroService;
            _logger = logger;
            _employeeAccessService = employeeAccessService;
        }

        [Route("QuanLyNhaTro/ChotDienNuoc")]
        [ApiExplorerSettings(IgnoreApi = true)]
        public async Task<IActionResult> Index()
        {
            ViewBag.ListChiNhanh = await _phongTroService.GetDanhSachChiNhanhDropdownAsync();

            var chuaPhanCong = false;
            if (User.IsInRole("NhanVien") && int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId))
            {
                var scope = await _employeeAccessService.GetScopeAsync(actorId);
                chuaPhanCong = scope != null && !scope.IsAdmin && scope.ActiveBranchIds.Count == 0;
            }
            ViewBag.ChuaPhanCongChiNhanh = chuaPhanCong;

            return View();
        }

        [HttpGet("/DienNuoc/GetDanhSachPhongs")]
        public async Task<IActionResult> GetDanhSachPhongs(int chiNhanhId, int thang, int nam)
        {
            if (chiNhanhId <= 0 || thang < 1 || thang > 12 || nam < 2000)
            {
                return BadRequest(new { Message = "Tham số lọc không hợp lệ." });
            }

            var actorIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(actorIdStr, out var actorId) || actorId <= 0)
            {
                return Unauthorized(new { Message = "Người dùng chưa đăng nhập hoặc phiên làm việc đã hết hạn." });
            }

            try
            {
                var data = await _dienNuocService.GetDanhSachDienNuocAsync(chiNhanhId, thang, nam, actorId);
                return Ok(data);
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy danh sách điện nước chi nhánh {ChiNhanhId}, tháng {Thang}/{Nam}", chiNhanhId, thang, nam);
                return StatusCode(500, new { Message = "Có lỗi xảy ra trên máy chủ khi lấy dữ liệu." });
            }
        }

        [HttpPost("/DienNuoc/SaveChotDienNuoc")]
        public async Task<IActionResult> SaveChotDienNuoc([FromBody] ChotDienNuocReq request)
        {
            var actorIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(actorIdStr, out var actorId) || actorId <= 0)
            {
                return Unauthorized(new { Message = "Người dùng chưa đăng nhập hoặc phiên làm việc đã hết hạn." });
            }

            try
            {
                var result = await _dienNuocService.SaveChotDienNuocAsync(request, actorId);
                if (!result.IsSuccess)
                {
                    return BadRequest(new { Message = result.ErrorMessage });
                }
                return Ok(new { Message = "Lưu chỉ số điện nước thành công!" });
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lưu chốt điện nước chi nhánh {ChiNhanhId}, tháng {Thang}/{Nam}", request?.ChiNhanhId, request?.Thang, request?.Nam);
                return StatusCode(500, new { Message = "Lỗi hệ thống khi lưu chốt điện nước." });
            }
        }
    }
}

