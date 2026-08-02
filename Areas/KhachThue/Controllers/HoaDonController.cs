using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyChoThuePhongTroWeb.Data;
using System.Linq;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Services.HoaDons;
using Microsoft.Extensions.Configuration;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Helpers;

namespace QuanLyChoThuePhongTroWeb.Areas.KhachThue.Controllers
{
    [Area("KhachThue")]
    [Authorize]
    public class HoaDonController : Controller
    {
        private readonly IHoaDonService _hoaDonService;
        private readonly IConfiguration _configuration;

        public HoaDonController(IHoaDonService hoaDonService, IConfiguration configuration)
        {
            _hoaDonService = hoaDonService;
            _configuration = configuration;
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

            var hoaDons = await _hoaDonService.GetHoaDonsByNguoiThueIdAsync(nguoiThueId);
            var result = hoaDons.Select(h => new {
                h.HoaDonId,
                h.MaHoaDon,
                h.Thang,
                h.Nam,
                h.TongTien,
                TrangThaiHoaDon = h.TrangThaiHoaDonValue,
                NgayTao = h.NgayTao
            });

            return Json(result);
        }

        [HttpGet]
        public async Task<IActionResult> XemChiTiet(int id)
        {
            var nguoiThueIdClaim = User.FindFirst("NguoiThueId");
            if (nguoiThueIdClaim == null) return Unauthorized();
            int nguoiThueId = int.Parse(nguoiThueIdClaim.Value);

            // Kiểm tra hóa đơn này có thuộc về hợp đồng của người thuê không
            var isOwn = await _hoaDonService.CheckHoaDonOwnershipAsync(id, nguoiThueId);

            if (!isOwn) return NotFound("Không tìm thấy hóa đơn hoặc không có quyền truy cập.");

            var data = await _hoaDonService.GetHoaDonByIdAsync(id);
            if (data == null) return NotFound("Không tìm thấy dữ liệu chi tiết hóa đơn.");

            return Json(new {
                hoaDonId = data.HoaDonId,
                maHoaDon = data.MaHoaDon,
                ngayTao = data.NgayTao,
                tenPhong = data.TenPhong,
                thang = data.Thang,
                nam = data.Nam,
                tenNguoiThue = data.TenNguoiThue,
                trangThaiHoaDon = data.TrangThaiHoaDon,
                tongTien = data.TongTien,
                chiTietHoaDons = data.ChiTietHoaDons,
                lichSuThanhToans = data.LichSuThanhToans,
                bankId = _configuration["VietQRSettings:BankId"] ?? "MB",
                accountNumber = _configuration["VietQRSettings:AccountNumber"] ?? "",
                accountName = _configuration["VietQRSettings:AccountName"] ?? ""
            });
        }

        [HttpGet("/KhachThue/HoaDon/GetVietQR")]
        public async Task<IActionResult> GetVietQR([FromQuery] int hoaDonId)
        {
            var nguoiThueIdClaim = User.FindFirst("NguoiThueId");
            if (nguoiThueIdClaim == null) return Unauthorized();
            int nguoiThueId = int.Parse(nguoiThueIdClaim.Value);

            var isOwn = await _hoaDonService.CheckHoaDonOwnershipAsync(hoaDonId, nguoiThueId);
            if (!isOwn) return NotFound("Không tìm thấy hóa đơn hoặc không có quyền truy cập.");

            var hd = await _hoaDonService.GetHoaDonByIdAsync(hoaDonId);
            if (hd == null) return NotFound(new { Message = "Không tìm thấy hóa đơn." });

            if (hd.TrangThaiHoaDon != "Chưa thanh toán")
            {
                return BadRequest(new { Message = "Hóa đơn đã được thanh toán hoặc không hợp lệ." });
            }

            var bankId = _configuration["VietQRSettings:BankId"] ?? "MB";
            var accountNumber = _configuration["VietQRSettings:AccountNumber"] ?? "";
            string memo = $"THANH TOAN {hd.MaHoaDon}";

            string qrString = VietQRHelper.GenerateVietQRString(bankId, accountNumber, hd.TongTien, memo);
            byte[] qrBytes = VietQRHelper.GenerateQRCodePNGBytes(qrString);

            return File(qrBytes, "image/png");
        }
    }
}
