using Microsoft.AspNetCore.Mvc;
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

        public DienNuocController(IDienNuocService dienNuocService, IPhongTroService phongTroService)
        {
            _dienNuocService = dienNuocService;
            _phongTroService = phongTroService;
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
                return BadRequest(new { Message = "Tham số không hợp lệ." });
            }

            var data = await _dienNuocService.GetDanhSachDienNuocAsync(chiNhanhId, thang, nam);
            return Ok(data);
        }

        [HttpPost("/DienNuoc/SaveChotDienNuoc")]
        public async Task<IActionResult> SaveChotDienNuoc([FromBody] ChotDienNuocReq request)
        {
            var result = await _dienNuocService.SaveChotDienNuocAsync(request);
            if (!result.IsSuccess)
            {
                return BadRequest(new { Message = result.ErrorMessage });
            }
            return Ok(new { Message = "Lưu chỉ số điện nước thành công!" });
        }
    }
}
