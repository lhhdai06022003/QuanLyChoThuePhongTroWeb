using Microsoft.AspNetCore.Mvc;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Services.PhongTros;
using QuanLyChoThuePhongTroWeb.Models;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Controllers
{
    [Area("QuanLyNhaTro")]
    [Route("[controller]/[action]")]
    [Microsoft.AspNetCore.Authorization.Authorize]
    public class PhongTrosController : Controller
    {
        private readonly IPhongTroService _phongTroService;

        public PhongTrosController(IPhongTroService phongTroService)
        {
            _phongTroService = phongTroService;
        }

        public async Task<IActionResult> QuanLyPhongTro()
        {
            ViewBag.ChiNhanhs = await _phongTroService.GetDanhSachChiNhanhDropdownAsync();
            return View();
        }

        public async Task<IActionResult> SoDoPhong()
        {
            ViewBag.ChiNhanhs = await _phongTroService.GetDanhSachChiNhanhDropdownAsync();
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetSoDoPhong([FromQuery] int chiNhanhId = 0)
        {
            var data = await _phongTroService.GetSoDoPhongAsync(chiNhanhId);
            return Json(data);
        }

        [HttpGet]
        public async Task<IActionResult> DanhSachPhongTro()
        {
            var data = await _phongTroService.GetDanhSachPhongTroAsync();
            return Json(new { data });
        }

        [HttpGet]
        public async Task<IActionResult> GetPhongTro(int id)
        {
            var result = await _phongTroService.GetPhongTroByIdAsync(id);
            return Json(new { success = result.Success, message = result.Message, data = result.Data });
        }

        [HttpPost]
        public async Task<IActionResult> ThemPhongTro(PhongTro model)
        {
            try
            {
                var result = await _phongTroService.ThemPhongTroAsync(model);
                return Json(new { success = result.Success, message = result.Message });
            }
            catch (Exception ex)
            {
                // Bọc try-catch ở Controller để chống sập server, có thể log lại ex.Message
                return Json(new { success = false, message = "Lỗi hệ thống khi thêm mới!" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> CapNhatPhongTro(PhongTro model)
        {
            try
            {
                var result = await _phongTroService.CapNhatPhongTroAsync(model);
                return Json(new { success = result.Success, message = result.Message });
            }
            catch (Exception)
            {
                return Json(new { success = false, message = "Lỗi hệ thống khi cập nhật!" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> XoaPhongTro(int id)
        {
            try
            {
                var result = await _phongTroService.XoaPhongTroAsync(id);
                return Json(new { success = result.Success, message = result.Message });
            }
            catch (Exception)
            {
                return Json(new { success = false, message = "Lỗi hệ thống khi xóa!" });
            }
        }
    }
}
