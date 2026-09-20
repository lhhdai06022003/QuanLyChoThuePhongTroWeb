using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuanLyChoThuePhongTroWeb.Models;
using System.Threading.Tasks;

namespace QuanLyChoThuePhongTroWeb.Areas.KhachThue.Controllers
{
    [Area("KhachThue")]
    [Authorize]
    public class HopDongController : Controller
    {
        private readonly IHopDongService _hopDongService;

        public HopDongController(IHopDongService hopDongService)
        {
            _hopDongService = hopDongService;
        }

        public IActionResult Index()
        {
            if (!User.IsInRole("KhachThue"))
            {
                return Content("LỖI HỆ THỐNG: Truy cập bị từ chối. Tài khoản của bạn không có quyền Khách Thuê.");
            }

            var nguoiThueIdClaim = User.FindFirst("NguoiThueId");
            if (nguoiThueIdClaim == null) return Content("LỖI HỆ THỐNG: Tài khoản của bạn không được liên kết với hồ sơ người thuê nào (NguoiThueId bị trống). Vui lòng liên hệ Admin.");

            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetDanhSach()
        {
            var nguoiThueIdClaim = User.FindFirst("NguoiThueId");
            if (nguoiThueIdClaim == null) return Unauthorized();
            int nguoiThueId = int.Parse(nguoiThueIdClaim.Value);

            var hopDongs = await _hopDongService.GetHopDongsByNguoiThueIdAsync(nguoiThueId);

            var result = hopDongs.Select(h => new {
                h.HopDongId,
                h.MaHopDong,
                SoPhong = h.SoPhong,
                TenChiNhanh = h.TenChiNhanh,
                ThoiDiemBatDau = h.ThoiDiemBatDau.ToString("dd/MM/yyyy"),
                ThoiDiemKetThuc = h.ThoiDiemKetThuc?.ToString("dd/MM/yyyy") ?? "Vô thời hạn",
                h.TienThuePhong,
                TrangThaiHopDong = (int)h.TrangThaiHopDong
            });

            return Json(result);
        }

        [HttpGet]
        public async Task<IActionResult> XemChiTiet(int id)
        {
            var nguoiThueIdClaim = User.FindFirst("NguoiThueId");
            if (nguoiThueIdClaim == null) return Unauthorized();
            int nguoiThueId = int.Parse(nguoiThueIdClaim.Value);

            var data = await _hopDongService.GetChiTietHopDongKhachThueAsync(id, nguoiThueId);

            if (data == null) return NotFound("Không tìm thấy hợp đồng hoặc không có quyền truy cập.");

            return Json(data);
        }
    }
}
