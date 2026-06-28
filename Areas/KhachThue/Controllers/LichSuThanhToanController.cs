using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyChoThuePhongTroWeb.Data;
using System.Linq;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Services.LichSuThanhToans;
using System.Threading.Tasks;

namespace QuanLyChoThuePhongTroWeb.Areas.KhachThue.Controllers
{
    [Area("KhachThue")]
    [Authorize]
    public class LichSuThanhToanController : Controller
    {
        private readonly ILichSuThanhToanService _lichSuThanhToanService;

        public LichSuThanhToanController(ILichSuThanhToanService lichSuThanhToanService)
        {
            _lichSuThanhToanService = lichSuThanhToanService;
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

            var lichSuThanhToans = await _lichSuThanhToanService.GetLichSuByNguoiThueIdAsync(nguoiThueId);

            var result = lichSuThanhToans.Select(l => new {
                l.LichSuThanhToanId,
                l.MaGiaoDich,
                MaHoaDon = l.HoaDon?.MaHoaDon ?? "",
                SoPhong = l.HoaDon?.HopDong?.PhongTro?.SoPhong ?? "",
                l.SoTienThanhToan,
                PhuongThucThanhToan = (int)l.PhuongThucThanhToan,
                NgayThanhToan = l.NgayThanhToan.ToString("dd/MM/yyyy HH:mm"),
                GhiChu = l.GhiChu ?? "-"
            });

            return Json(result);
        }
    }
}
