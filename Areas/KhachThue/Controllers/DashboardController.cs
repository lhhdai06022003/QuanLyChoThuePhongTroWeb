using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuanLyChoThuePhongTroWeb.Data;
using System.Security.Claims;

namespace QuanLyChoThuePhongTroWeb.Areas.KhachThue.Controllers
{
    [Area("KhachThue")]
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DashboardController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
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

            var nguoiThue = _context.NguoiThues.Find(nguoiThueId);
            ViewBag.NguoiThueName = nguoiThue?.HoVaTen ?? "Khách";
            ViewBag.NguoiThueId = nguoiThueId;
            return View();
        }
    }
}
