using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyChoThuePhongTroWeb.Data;
using QuanLyChoThuePhongTroWeb.Models;
using System.Linq;
using System.Threading.Tasks;

namespace QuanLyChoThuePhongTroWeb.Areas.KhachThue.Controllers
{
    [Area("KhachThue")]
    [Authorize]
    public class HopDongController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HopDongController(ApplicationDbContext context)
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

            // Tìm hợp đồng mà người thuê làm đại diện (có thể có nhiều nhưng thường lấy cái đang hoạt động)
            var hopDongs = await _context.HopDongs
                .Include(x => x.PhongTro).ThenInclude(p => p.ChiNhanh)
                .Include(x => x.NguoiThue)
                .Include(x => x.HopDongDieuKhoans)
                .Where(x => x.NguoiThueId == nguoiThueId && !x.IsDeleted)
                .OrderByDescending(x => x.NgayTao)
                .ToListAsync();

            return View(hopDongs);
        }

        [HttpGet]
        public async Task<IActionResult> XemChiTiet(int id)
        {
            var nguoiThueIdClaim = User.FindFirst("NguoiThueId");
            if (nguoiThueIdClaim == null) return Unauthorized();
            int nguoiThueId = int.Parse(nguoiThueIdClaim.Value);

            var hopDong = await _context.HopDongs
                .Include(h => h.NguoiThue)
                .Include(h => h.PhongTro)
                .FirstOrDefaultAsync(h => h.HopDongId == id && h.NguoiThueId == nguoiThueId && !h.IsDeleted);

            if (hopDong == null) return NotFound("Không tìm thấy hợp đồng.");

            var members = await _context.ChiTietThanhVienHopDongs
                .Include(m => m.NguoiThue)
                .Where(m => m.HopDongId == id && !m.IsDeleted)
                .Select(m => new {
                    hoVaTen = m.NguoiThue.HoVaTen,
                    soDienThoai = m.NguoiThue.SoDienThoai,
                    cccd = m.NguoiThue.CCCD,
                    ngayVao = m.NgayVao.ToString("dd/MM/yyyy")
                }).ToListAsync();

            var services = await _context.DangKyDichVus
                .Include(s => s.DichVuChiNhanh).ThenInclude(dcn => dcn.DichVu)
                .Where(s => s.PhongTroId == hopDong.PhongTroId)
                .Select(s => new {
                    tenDichVu = s.DichVuChiNhanh.DichVu.TenDichVu,
                    donGia = s.DichVuChiNhanh.GiaDichVu,
                    donVi = s.DichVuChiNhanh.DichVu.DonVi,
                    soLuong = s.SoLuong
                }).ToListAsync();

            var data = new {
                maHopDong = hopDong.MaHopDong,
                thoiDiemBatDau = hopDong.ThoiDiemBatDau.ToString("dd/MM/yyyy"),
                thoiDiemKetThuc = hopDong.ThoiDiemKetThuc?.ToString("dd/MM/yyyy") ?? "Không thời hạn",
                tienCocPhong = hopDong.TienCocPhong,
                tienThuePhong = hopDong.TienThuePhong,
                tenNguoiDaiDien = hopDong.NguoiThue?.HoVaTen,
                soDienThoaiDaiDien = hopDong.NguoiThue?.SoDienThoai,
                cccdDaiDien = hopDong.NguoiThue?.CCCD,
                members = members,
                services = services
            };

            return Json(data);
        }
    }
}
