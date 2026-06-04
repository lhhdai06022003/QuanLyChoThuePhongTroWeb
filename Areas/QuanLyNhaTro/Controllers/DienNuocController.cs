using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Services.DienNuocs;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Services.PhongTros;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels.Requests;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Controllers
{
    [Area("QuanLyNhaTro")]
    [ApiController]
    [Microsoft.AspNetCore.Authorization.Authorize]
    public class DienNuocController : Controller
    {
        private readonly IDienNuocService _dienNuocService;
        private readonly IPhongTroService _phongTroService;
        private readonly ILogger<DienNuocController> _logger;

        public DienNuocController(IDienNuocService dienNuocService, IPhongTroService phongTroService, ILogger<DienNuocController> logger)
        {
            _dienNuocService = dienNuocService;
            _phongTroService = phongTroService;
            _logger = logger;
        }

        [Route("QuanLyNhaTro/ChotDienNuoc")]
        [ApiExplorerSettings(IgnoreApi = true)]
        public async Task<IActionResult> Index()
        {
            ViewBag.ListChiNhanh = await _phongTroService.GetDanhSachChiNhanhDropdownAsync();
            return View();
        }

        [HttpGet("/DienNuoc/GetDanhSachPhongs")]
        public async Task<IActionResult> GetDanhSachPhongs(int chiNhanhId, int thang, int nam)
        {
            if (chiNhanhId <= 0 || thang < 1 || thang > 12 || nam < 2000)
            {
                return BadRequest(new { Message = "Tham số lọc không hợp lệ." });
            }

            try
            {
                var data = await _dienNuocService.GetDanhSachDienNuocAsync(chiNhanhId, thang, nam);
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
            try
            {
                var result = await _dienNuocService.SaveChotDienNuocAsync(request);
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
