using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyChoThuePhongTroWeb.Data;
using System.Linq;
using System.Threading.Tasks;

namespace QuanLyChoThuePhongTroWeb.Areas.KhachThue.Controllers
{
    [Area("KhachThue")]
    [Authorize]
    public class LichSuThanhToanController : Controller
    {
        private readonly ApplicationDbContext _context;

        public LichSuThanhToanController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            if (!User.IsInRole("KhachThue"))
            {
                return Content("LỖI HỆ THỐNG: Truy cập bị từ chối. Tài khoản của bạn không có quyền Khách Thuê.");
            }

            var nguoiThueIdClaim = User.FindFirst("NguoiThueId");
            if (nguoiThueIdClaim == null) return Content("LỖI HỆ THỐNG: Tài khoản của bạn không được liên kết với hồ sơ người thuê nào (NguoiThueId bị trống). Vui lòng liên hệ Admin.");

            int nguoiThueId = int.Parse(nguoiThueIdClaim.Value);

            // Tìm lịch sử thanh toán mà hóa đơn thuộc về hợp đồng của người thuê này
            var lichSuThanhToans = await _context.LichSuThanhToans
                .Include(ls => ls.HoaDon).ThenInclude(hd => hd.HopDong).ThenInclude(h => h.PhongTro)
                .Where(ls => ls.HoaDon.HopDong.NguoiThueId == nguoiThueId && !ls.IsDeleted)
                .OrderByDescending(ls => ls.NgayThanhToan)
                .ToListAsync();

            return View(lichSuThanhToans);
        }
    }
}
