using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Services;

namespace QuanLyChoThuePhongTroWeb.Areas.KhachThue.Controllers
{
    [Area("KhachThue")]
    [Authorize]
    public class HoaDonController : Controller
    {
        private readonly IHoaDonService _hoaDonService;
        private readonly IInvoiceViewService _invoiceViewService;
        private readonly IConfiguration _configuration;
        private readonly IVietQRService _vietQRService;

        public HoaDonController(
            IHoaDonService hoaDonService,
            IInvoiceViewService invoiceViewService,
            IConfiguration configuration,
            IVietQRService vietQRService)
        {
            _hoaDonService = hoaDonService;
            _invoiceViewService = invoiceViewService;
            _configuration = configuration;
            _vietQRService = vietQRService;
        }

        public IActionResult Index([FromQuery] int? hoaDonId)
        {
            if (!User.IsInRole("KhachThue"))
            {
                return Content("LỖI HỆ THỐNG: Truy cập bị từ chối. Tài khoản của bạn không có quyền Khách Thuê.");
            }

            var nguoiThueIdClaim = User.FindFirst("NguoiThueId");
            if (nguoiThueIdClaim == null) return Content("LỖI HỆ THỐNG: Tài khoản của bạn không được liên kết với hồ sơ người thuê nào (NguoiThueId bị trống). Vui lòng liên hệ Admin.");

            ViewBag.MoHoaDonId = (hoaDonId.HasValue && hoaDonId.Value > 0) ? hoaDonId.Value : 0;

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
                trangThai = h.TrangThaiHoaDon,
                trangThaiPhatHanh = h.TrangThaiPhatHanhValue,
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

            var data = await _invoiceViewService.GetTenantDetailAsync(id, nguoiThueId);
            if (data == null) return NotFound("Không tìm thấy hóa đơn hoặc không có quyền truy cập.");

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
                daHuy = data.DaHuy,
                lyDoHuy = data.LyDoHuy,
                coTheThanhToan = !data.DaHuy && data.TrangThaiHoaDon == "Chưa thanh toán",
                bankId = data.DaHuy ? "" : (_configuration["VietQRSettings:BankId"] ?? "MB"),
                accountNumber = data.DaHuy ? "" : (_configuration["VietQRSettings:AccountNumber"] ?? ""),
                accountName = data.DaHuy ? "" : (_configuration["VietQRSettings:AccountName"] ?? "")
            });
        }

        [HttpGet("/KhachThue/HoaDon/TaiPdf/{id:int}")]
        public async Task<IActionResult> TaiPdf(int id)
        {
            var nguoiThueIdClaim = User.FindFirst("NguoiThueId");
            if (nguoiThueIdClaim == null) return Unauthorized();
            if (!int.TryParse(nguoiThueIdClaim.Value, out int nguoiThueId)) return Unauthorized();

            var bytes = await _invoiceViewService.ExportTenantPdfAsync(id, nguoiThueId);
            if (bytes == null) return NotFound("Không tìm thấy hóa đơn hoặc không có quyền truy cập.");

            return File(bytes, "application/pdf", $"HoaDon_{id}.pdf");
        }

        [HttpGet("/KhachThue/HoaDon/GetVietQR")]
        public async Task<IActionResult> GetVietQR([FromQuery] int hoaDonId)
        {
            var nguoiThueIdClaim = User.FindFirst("NguoiThueId");
            if (nguoiThueIdClaim == null) return Unauthorized();
            int nguoiThueId = int.Parse(nguoiThueIdClaim.Value);

            var canPay = await _hoaDonService.CanTenantRequestPaymentAsync(hoaDonId, nguoiThueId);
            if (!canPay) return BadRequest(new { Message = "Hóa đơn không ở trạng thái cho phép thanh toán hoặc không tồn tại." });

            var hd = await _hoaDonService.GetHoaDonByIdAsync(hoaDonId);
            if (hd == null) return NotFound(new { Message = "Không tìm thấy hóa đơn." });

            var bankId = _configuration["VietQRSettings:BankId"] ?? "MB";
            var accountNumber = _configuration["VietQRSettings:AccountNumber"] ?? "";
            string memo = $"THANH TOAN {hd.MaHoaDon}";

            string qrString = _vietQRService.GenerateVietQRString(bankId, accountNumber, hd.TongTien, memo);
            byte[] qrBytes = _vietQRService.GenerateQRCodePNGBytes(qrString);

            return File(qrBytes, "image/png");
        }
    }
}
