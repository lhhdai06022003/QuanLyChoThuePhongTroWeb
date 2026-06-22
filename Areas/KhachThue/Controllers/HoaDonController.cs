using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyChoThuePhongTroWeb.Data;
using System.Linq;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Services.HoaDons;
using Microsoft.Extensions.Configuration;

namespace QuanLyChoThuePhongTroWeb.Areas.KhachThue.Controllers
{
    [Area("KhachThue")]
    [Authorize]
    public class HoaDonController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IHoaDonService _hoaDonService;
        private readonly IConfiguration _configuration;

        public HoaDonController(ApplicationDbContext context, IHoaDonService hoaDonService, IConfiguration configuration)
        {
            _context = context;
            _hoaDonService = hoaDonService;
            _configuration = configuration;
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

            // Tìm các hợp đồng của người thuê
            var hopDongIds = await _context.HopDongs
                .Where(x => x.NguoiThueId == nguoiThueId && !x.IsDeleted)
                .Select(x => x.HopDongId)
                .ToListAsync();

            ViewBag.BankId = _configuration["VietQRSettings:BankId"] ?? "MB";
            ViewBag.AccountNumber = _configuration["VietQRSettings:AccountNumber"] ?? "";
            ViewBag.AccountName = _configuration["VietQRSettings:AccountName"] ?? "";

            if (!hopDongIds.Any())
            {
                return View(Enumerable.Empty<QuanLyChoThuePhongTroWeb.Models.HoaDon>());
            }

            var hoaDons = await _context.HoaDons
                .Include(x => x.HopDong).ThenInclude(h => h.PhongTro)
                .Where(x => hopDongIds.Contains(x.HopDongId) && !x.IsDeleted)
                .OrderByDescending(x => x.NgayTao)
                .ToListAsync();

            return View(hoaDons);
        }

        [HttpGet]
        public async Task<IActionResult> XemChiTiet(int id)
        {
            var nguoiThueIdClaim = User.FindFirst("NguoiThueId");
            if (nguoiThueIdClaim == null) return Unauthorized();
            int nguoiThueId = int.Parse(nguoiThueIdClaim.Value);

            // Kiểm tra hóa đơn này có thuộc về hợp đồng của người thuê không
            var isOwn = await _context.HoaDons
                .Include(h => h.HopDong)
                .AnyAsync(h => h.HoaDonId == id && h.HopDong.NguoiThueId == nguoiThueId && !h.IsDeleted);

            if (!isOwn) return NotFound("Không tìm thấy hóa đơn hoặc không có quyền truy cập.");

            var data = await _hoaDonService.GetHoaDonByIdAsync(id);
            if (data == null) return NotFound("Không tìm thấy dữ liệu chi tiết hóa đơn.");

            return Json(data);
        }
    }
}
