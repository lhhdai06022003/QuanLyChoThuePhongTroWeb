using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
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
        private readonly Data.ApplicationDbContext _context;
        private readonly IConfiguration _configuration;

        public PhongTrosController(IPhongTroService phongTroService, Data.ApplicationDbContext context, IConfiguration configuration)
        {
            _phongTroService = phongTroService;
            _context = context;
            _configuration = configuration;
        }

        public async Task<IActionResult> QuanLyPhongTro()
        {
            ViewBag.ChiNhanhs = await _phongTroService.GetDanhSachChiNhanhDropdownAsync();
            var section = _configuration.GetSection("VietQRSettings");
            ViewBag.BankId = section["BankId"] ?? "";
            ViewBag.AccountNumber = section["AccountNumber"] ?? "";
            ViewBag.AccountName = section["AccountName"] ?? "";
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

        [HttpPost]
        public async Task<IActionResult> PhatSinhNgauNhien()
        {
            try
            {
                var context = HttpContext.RequestServices.GetRequiredService<Data.ApplicationDbContext>();
                var chiNhanhs = await context.ChiNhanhs.Where(c => !c.IsDeleted).ToListAsync();
                if (!chiNhanhs.Any())
                {
                    return Json(new { success = false, message = "Không tìm thấy chi nhánh nào trong database. Vui lòng thêm chi nhánh trước!" });
                }

                var random = new Random();
                var addedRooms = new List<string>();

                // Get current maximum rooms for each branch in memory
                var branchMaxRooms = new Dictionary<int, int>();
                foreach (var cn in chiNhanhs)
                {
                    var maxRoom = await context.PhongTros
                        .Where(p => p.ChiNhanhId == cn.ChiNhanhId)
                        .Select(p => p.SoPhong)
                        .ToListAsync();

                    int maxVal = 100;
                    if (maxRoom.Any())
                    {
                        maxVal = maxRoom
                            .Select(sp => int.TryParse(sp, out var n) ? n : 100)
                            .Max();
                    }
                    branchMaxRooms[cn.ChiNhanhId] = maxVal;
                }

                for (int i = 0; i < 10; i++)
                {
                    // Pick a random branch
                    var chiNhanh = chiNhanhs[random.Next(chiNhanhs.Count)];
                    
                    int nextRoomNum = branchMaxRooms[chiNhanh.ChiNhanhId] + 1;
                    branchMaxRooms[chiNhanh.ChiNhanhId] = nextRoomNum;

                    var pt = new PhongTro
                    {
                        ChiNhanhId = chiNhanh.ChiNhanhId,
                        SoPhong = nextRoomNum.ToString(),
                        TangLau = (nextRoomNum / 100) == 0 ? 1 : (nextRoomNum / 100),
                        GiaThue = random.Next(18, 45) * 100000, // 1.8tr to 4.5tr
                        DienTich = random.Next(15, 30),
                        SoNguoiToiDa = random.Next(2, 4),
                        TrangThai = TrangThaiPhong.Trong,
                        MoTa = $"Phòng {nextRoomNum} ngẫu nhiên thuộc {chiNhanh.TenChiNhanh}",
                        NgayTao = DateTime.UtcNow
                    };
                    context.PhongTros.Add(pt);
                    addedRooms.Add($"{pt.SoPhong} ({chiNhanh.TenChiNhanh})");
                }

                await context.SaveChangesAsync();
                return Json(new { success = true, message = $"Đã phát sinh 10 phòng thành công: {string.Join(", ", addedRooms)}" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Lỗi hệ thống: {ex.Message}" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetQuickContract(int phongTroId)
        {
            if (phongTroId <= 0) return BadRequest(new { message = "ID phòng không hợp lệ." });

            var contract = await _context.HopDongs
                .Include(h => h.NguoiThue)
                .Include(h => h.PhongTro)
                .Where(h => h.PhongTroId == phongTroId && h.TrangThaiHopDong == TrangThaiHopDong.DangHoatDong && !h.IsDeleted)
                .FirstOrDefaultAsync();

            if (contract == null)
            {
                return NotFound(new { message = "Không tìm thấy hợp đồng đang hoạt động cho phòng này." });
            }

            // Lấy danh sách thành viên ở ghép
            var members = await _context.ChiTietThanhVienHopDongs
                .Include(m => m.NguoiThue)
                .Where(m => m.HopDongId == contract.HopDongId && m.NgayChuyenDi == null && !m.IsDeleted)
                .Select(m => new {
                    hoVaTen = m.NguoiThue.HoVaTen,
                    soDienThoai = m.NguoiThue.SoDienThoai,
                    cccd = m.NguoiThue.CCCD,
                    ngayVao = m.NgayVao.ToString("dd/MM/yyyy")
                })
                .ToListAsync();

            // Lấy dịch vụ đăng ký
            var services = await _context.DangKyDichVus
                .Include(s => s.DichVuChiNhanh.DichVu)
                .Where(s => s.PhongTroId == phongTroId && !s.DichVuChiNhanh.IsDeleted)
                .Select(s => new {
                    tenDichVu = s.DichVuChiNhanh.DichVu.TenDichVu,
                    donGia = s.DichVuChiNhanh.GiaDichVu,
                    donVi = s.DichVuChiNhanh.DichVu.DonVi,
                    soLuong = s.SoLuong
                })
                .ToListAsync();

            return Ok(new {
                hopDongId = contract.HopDongId,
                maHopDong = contract.MaHopDong,
                thoiDiemBatDau = contract.ThoiDiemBatDau.ToString("dd/MM/yyyy"),
                thoiDiemKetThuc = contract.ThoiDiemKetThuc.HasValue ? contract.ThoiDiemKetThuc.Value.ToString("dd/MM/yyyy") : "Không xác định",
                tienCocPhong = contract.TienCocPhong,
                tienThuePhong = contract.TienThuePhong,
                tenNguoiDaiDien = contract.NguoiThue.HoVaTen,
                soDienThoaiDaiDien = contract.NguoiThue.SoDienThoai,
                cccdDaiDien = contract.NguoiThue.CCCD,
                members = members,
                services = services
            });
        }

        [HttpGet]
        public async Task<IActionResult> GetUnpaidInvoice(int phongTroId)
        {
            if (phongTroId <= 0) return BadRequest(new { message = "ID phòng không hợp lệ." });

            var invoice = await _context.HoaDons
                .Include(i => i.HopDong)
                    .ThenInclude(h => h.PhongTro)
                .Include(i => i.HopDong)
                    .ThenInclude(h => h.NguoiThue)
                .Where(i => i.HopDong.PhongTroId == phongTroId && i.TrangThaiHoaDon == QuanLyChoThuePhongTroWeb.Models.TrangThaiHoaDon.ChuaThanhToan && !i.IsDeleted)
                .OrderByDescending(i => i.NgayTao)
                .FirstOrDefaultAsync();

            if (invoice == null)
            {
                return NotFound(new { message = "Không tìm thấy hóa đơn chưa thanh toán nào cho phòng này." });
            }

            var details = await _context.ChiTietHoaDons
                .Include(d => d.DichVu)
                .Where(d => d.HoaDonId == invoice.HoaDonId && !d.IsDeleted)
                .ToListAsync();

            var mappedDetails = details.Select(d => new {
                tenDichVu = d.TenDichVu,
                donGia = d.DonGia,
                soLuong = d.SoLuong,
                donVi = d.DichVu != null ? d.DichVu.DonVi : 
                        (d.TenDichVu.ToLower().Contains("điện") ? "kWh" : 
                        (d.TenDichVu.ToLower().Contains("nước") ? "m³" : "-")),
                tongTien = d.TongTien
            }).ToList();

            return Ok(new {
                hoaDonId = invoice.HoaDonId,
                maHoaDon = invoice.MaHoaDon,
                thang = invoice.Thang,
                nam = invoice.Nam,
                tongTien = invoice.TongTien,
                tenPhong = invoice.HopDong.PhongTro.SoPhong,
                tenNguoiThue = invoice.HopDong.NguoiThue.HoVaTen,
                chiTiets = mappedDetails
            });
        }
    }
}
