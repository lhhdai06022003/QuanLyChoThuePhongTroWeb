using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuanLyChoThuePhongTroWeb.Data;
using System.Security.Claims;

using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Services.NguoiThues;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Services.HoaDons;
using System.Threading.Tasks;
using System.Linq;
using QuanLyChoThuePhongTroWeb.Models;

namespace QuanLyChoThuePhongTroWeb.Areas.KhachThue.Controllers
{
    [Area("KhachThue")]
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly INguoiThueService _nguoiThueService;
        private readonly IHoaDonService _hoaDonService;

        public DashboardController(INguoiThueService nguoiThueService, IHoaDonService hoaDonService)
        {
            _nguoiThueService = nguoiThueService;
            _hoaDonService = hoaDonService;
        }

        public async Task<IActionResult> Index()
        {
            if (!User.IsInRole("KhachThue"))
            {
                return Content("LỖI HỆ THỐNG: Truy cập bị từ chối. Tài khoản của bạn không có quyền Khách Thuê.");
            }

            // Lấy ID người thuê từ Claims
            var nguoiThueIdClaim = User.FindFirst("NguoiThueId");
            if (nguoiThueIdClaim == null)
            {
                return Content("LỖI HỆ THỐNG: Tài khoản của bạn được gán Role Khách Thuê nhưng không được liên kết với hồ sơ người thuê nào (NguoiThueId bị trống trong database). Vui lòng liên hệ Admin.");
            }

            int nguoiThueId = int.Parse(nguoiThueIdClaim.Value);

            var nguoiThue = await _nguoiThueService.GetByIdAsync(nguoiThueId);
            ViewBag.NguoiThueName = nguoiThue?.HoVaTen ?? "Khách";
            ViewBag.NguoiThueId = nguoiThueId;

            // Lấy hóa đơn chưa thanh toán
            var hoaDons = await _hoaDonService.GetHoaDonsByNguoiThueIdAsync(nguoiThueId);
            double tongTienChuaThanhToan = hoaDons
                .Where(h => h.TrangThaiHoaDonValue == (int)TrangThaiHoaDon.ChuaThanhToan)
                .Sum(h => h.TongTien);
            
            ViewBag.TongTienChuaThanhToan = tongTienChuaThanhToan;

            return View();
        }
    }
}
