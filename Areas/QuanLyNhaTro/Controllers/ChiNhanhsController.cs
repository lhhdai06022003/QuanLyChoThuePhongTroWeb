using Microsoft.AspNetCore.Mvc;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Services.ChiNhanhs;
using QuanLyChoThuePhongTroWeb.Models;
using Microsoft.AspNetCore.Authorization;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Controllers
{
    [Route("[controller]/[action]")]
    public class ChiNhanhsController : AdminBaseController
    {
        private readonly IChiNhanhService _chiNhanhService;
        public ChiNhanhsController(IChiNhanhService chiNhanhService)
        {
            _chiNhanhService = chiNhanhService;
        }

        public async Task<IActionResult> QuanLyChiNhanh()
        {
            // 1. Gọi Service để lấy danh sách chi nhánh từ Database
            //var danhSachChiNhanh = await _chiNhanhService.GetAllAsync();

            // 2. Trả danh sách này về cho file View (Index.cshtml)
            return View();
        }
        [HttpGet]
        public async Task<IActionResult> DanhSachChiNhanh()
        {
            var danhSach = await _chiNhanhService.DanhSachChiNhanh();
            // DataTables mặc định yêu cầu dữ liệu phải nằm trong thuộc tính tên là "data"
            return Json(new { data = danhSach });
        }
        [HttpGet]
        public async Task<JsonResult> GetChiNhanh(int id)
        {
            var chiNhanh = await _chiNhanhService.GetChiNhanh(id);
            if (chiNhanh == null)
                return Json(new { success = false, message = "Không tìm thấy chi nhánh!" });

            return Json(new { success = true, data = chiNhanh });
        }
        [HttpPost]
        public async Task<IActionResult> ThemChiNhanhMoi(ChiNhanh model)
        {
            // Controller CHỈ NHẬN MODEL VÀ GỌI SERVICE
            var result = await _chiNhanhService.ThemChiNhanhMoi(model);

            // Trả về JSON cho Ajax
            return Json(new { success = result.IsSuccess, message = result.Message });
        }
        [HttpPost]
        public async Task<JsonResult> CapNhatChiNhanh(ChiNhanh model)
        {
            // Controller CHỈ NHẬN MODEL VÀ GỌI SERVICE
            var result = await _chiNhanhService.ThemChiNhanhMoi(model);

            // Trả về JSON cho Ajax
            return Json(new { success = result.IsSuccess, message = result.Message });
        }
        [HttpPost]
        public async Task<JsonResult> XoaChiNhanh(int id)
        {
            // Controller CHỈ NHẬN ID VÀ GỌI SERVICE
            var result = await _chiNhanhService.XoaChiNhanh(id);

            // Trả về JSON cho Ajax
            return Json(new { success = result.IsSuccess, message = result.Message });
        }
    }
}

