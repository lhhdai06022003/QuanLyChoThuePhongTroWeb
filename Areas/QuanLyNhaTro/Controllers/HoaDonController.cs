using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Services.Emails;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Services.HoaDons;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Services.PhongTros;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels.Requests;
using System.Security.Claims;

using Microsoft.Extensions.Logging;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Controllers
{
    [Area("QuanLyNhaTro")]
    [ApiController]
    [Authorize(Roles = "Admin,NhanVien")]
    [AutoValidateAntiforgeryToken]
    public class HoaDonController : Controller
    {
        private readonly IHoaDonService _hoaDonService;
        private readonly IPhongTroService _phongTroService;
        private readonly Microsoft.Extensions.Configuration.IConfiguration _configuration;
        private readonly IEmailService _emailService;
        private readonly ILogger<HoaDonController> _logger;

        public HoaDonController(
            IHoaDonService hoaDonService, 
            IPhongTroService phongTroService, 
            Microsoft.Extensions.Configuration.IConfiguration configuration, 
            IEmailService emailService,
            ILogger<HoaDonController> logger)
        {
            _hoaDonService = hoaDonService;
            _phongTroService = phongTroService;
            _configuration = configuration;
            _emailService = emailService;
            _logger = logger;
        }

        [Route("QuanLyNhaTro/QuanLyHoaDon")]
        [ApiExplorerSettings(IgnoreApi = true)]
        public async Task<IActionResult> Index()
        {
            ViewBag.ListChiNhanh = await _phongTroService.GetDanhSachChiNhanhDropdownAsync();
            ViewBag.BankId = _configuration["VietQRSettings:BankId"] ?? "MB";
            ViewBag.AccountNumber = _configuration["VietQRSettings:AccountNumber"] ?? "";
            ViewBag.AccountName = _configuration["VietQRSettings:AccountName"] ?? "";
            return View();
        }

        // Phát sinh hóa đơn hàng loạt hoặc theo phòng đã chọn
        [HttpPost("/HoaDon/PhatSinh")]
        public async Task<IActionResult> PhatSinh([FromBody] PhatSinhHoaDonReq req)
        {
            if (req.ChiNhanhId <= 0 || req.Thang < 1 || req.Thang > 12 || req.Nam < 2000)
                return BadRequest(new { Message = "Tham số không hợp lệ." });

            var result = await _hoaDonService.PhatSinhHoaDonAsync(req.ChiNhanhId, req.Thang, req.Nam, req.SelectedPhongTroIds);
            if (!result.IsSuccess)
                return BadRequest(new { Message = result.Message });

            return Ok(new { Message = result.Message, SoHoaDonMoi = result.SoHoaDonMoi });
        }

        // Xem trước phát sinh hóa đơn
        [HttpGet("/HoaDon/PreviewPhatSinh")]
        public async Task<IActionResult> PreviewPhatSinh([FromQuery] int chiNhanhId, [FromQuery] int thang, [FromQuery] int nam)
        {
            if (chiNhanhId <= 0 || thang < 1 || thang > 12 || nam < 2000)
                return BadRequest(new { Message = "Tham số không hợp lệ." });

            var data = await _hoaDonService.PreviewPhatSinhHoaDonAsync(chiNhanhId, thang, nam);
            return Ok(data);
        }

        // Cập nhật/Chỉnh sửa hóa đơn
        [HttpPost("/HoaDon/Update/{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateHoaDonReq req)
        {
            if (id <= 0 || req == null)
                return BadRequest(new { Message = "Dữ liệu không hợp lệ." });

            var result = await _hoaDonService.UpdateHoaDonAsync(id, req);
            if (!result.IsSuccess)
                return BadRequest(new { Message = result.ErrorMessage });

            return Ok(new { Message = "Cập nhật hóa đơn thành công!" });
        }

        // Danh sách hóa đơn (DataTable server-side)
        [HttpPost("/HoaDon/GetList")]
        public async Task<IActionResult> GetList([FromForm] int chiNhanhId = 0, [FromForm] int thang = 0, [FromForm] int nam = 0, [FromForm] int trangThai = -1)
        {
            var form = Request.Form;
            var request = new DataTableRequest
            {
                Draw = int.TryParse(form["draw"].FirstOrDefault(), out int d) ? d : 1,
                Start = int.TryParse(form["start"].FirstOrDefault(), out int s) ? s : 0,
                Length = int.TryParse(form["length"].FirstOrDefault(), out int l) ? l : 10,
                SearchValue = form["search[value]"].FirstOrDefault()
            };

            var data = await _hoaDonService.GetDanhSachHoaDonAsync(request, chiNhanhId, thang, nam, trangThai);
            return Ok(data);
        }

        // Chi tiết hóa đơn
        [HttpGet("/HoaDon/GetById/{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var data = await _hoaDonService.GetHoaDonByIdAsync(id);
            if (data == null) return NotFound(new { Message = "Không tìm thấy hóa đơn." });
            return Ok(data);
        }

        // Thu tiền
        [HttpPost("/HoaDon/ThuTien")]
        public async Task<IActionResult> ThuTien([FromBody] ThuTienReq req)
        {
            int userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var result = await _hoaDonService.ThuTienAsync(req.HoaDonId, req.PhuongThucThanhToan, req.GhiChu, userId);
            if (!result.IsSuccess) return BadRequest(new { Message = result.ErrorMessage });
            return Ok(new { Message = "Thu tiền thành công!" });
        }

        // Xóa hóa đơn
        [HttpDelete("/HoaDon/Delete/{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var result = await _hoaDonService.DeleteHoaDonAsync(id);
            if (!result.IsSuccess) return BadRequest(new { Message = result.ErrorMessage });
            return Ok(new { Message = "Đã xóa hóa đơn." });
        }

        // Xuất Excel
        [HttpGet("/HoaDon/ExportExcel/{id}")]
        [ApiExplorerSettings(IgnoreApi = true)]
        public async Task<IActionResult> ExportExcel(int id)
        {
            var bytes = await _hoaDonService.ExportExcelAsync(id);
            if (bytes == null) return NotFound();
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"HoaDon_{id}.xlsx");
        }

        // Xuất PDF
        [HttpGet("/HoaDon/ExportPdf/{id}")]
        [ApiExplorerSettings(IgnoreApi = true)]
        public async Task<IActionResult> ExportPdf(int id)
        {
            var bytes = await _hoaDonService.ExportPdfAsync(id);
            if (bytes == null) return NotFound();
            return File(bytes, "application/pdf", $"HoaDon_{id}.pdf");
        }

        // Endpoint sinh mã QR VietQR ngoại tuyến
        [HttpGet("/HoaDon/GetVietQR")]
        public async Task<IActionResult> GetVietQR([FromQuery] int hoaDonId)
        {
            var hd = await _hoaDonService.GetHoaDonByIdAsync(hoaDonId);
            if (hd == null) return NotFound(new { Message = "Không tìm thấy hóa đơn." });

            // Chỉ hiển thị QR nếu chưa thanh toán
            if (hd.TrangThaiHoaDon != "Chưa thanh toán")
            {
                return BadRequest(new { Message = "Hóa đơn đã được thanh toán hoặc không hợp lệ." });
            }

            var bankId = _configuration["VietQRSettings:BankId"] ?? "MB";
            var accountNumber = _configuration["VietQRSettings:AccountNumber"] ?? "";
            string memo = $"THANH TOAN {hd.MaHoaDon}";

            string qrString = Helpers.VietQRHelper.GenerateVietQRString(bankId, accountNumber, hd.TongTien, memo);
            byte[] qrBytes = Helpers.VietQRHelper.GenerateQRCodePNGBytes(qrString);

            return File(qrBytes, "image/png");
        }

        // Lấy danh sách hóa đơn chưa thanh toán để gửi mail hàng loạt
        [HttpGet("/HoaDon/GetUnpaidList")]
        public async Task<IActionResult> GetUnpaidList([FromQuery] int chiNhanhId, [FromQuery] int thang, [FromQuery] int nam)
        {
            try
            {
                var data = await _hoaDonService.GetDanhSachHoaDonChuaThanhToanAsync(chiNhanhId, thang, nam);
                return Ok(data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi tải danh sách hóa đơn chưa thanh toán.");
                return StatusCode(500, new { Message = "Lỗi hệ thống khi tải danh sách hóa đơn chưa thanh toán." });
            }
        }

        // Gửi email hóa đơn
        [HttpPost("/HoaDon/SendEmail/{id}")]
        public async Task<IActionResult> SendEmail(int id)
        {
            try
            {
                var hd = await _hoaDonService.GetHoaDonByIdAsync(id);
                if (hd == null) 
                    return NotFound(new { Message = "Không tìm thấy hóa đơn." });

                if (string.IsNullOrWhiteSpace(hd.Email))
                    return BadRequest(new { Message = "Khách thuê chưa đăng ký địa chỉ email." });

                // Xuất file PDF hóa đơn
                var pdfBytes = await _hoaDonService.ExportPdfAsync(id);
                if (pdfBytes == null || pdfBytes.Length == 0)
                    return BadRequest(new { Message = "Không thể sinh tệp PDF hóa đơn." });

                // Gọi EmailService để gửi thư
                var result = await _emailService.SendInvoiceEmailAsync(hd.Email, hd, pdfBytes);
                if (!result.IsSuccess)
                    return BadRequest(new { Message = $"Gửi email thất bại: {result.ErrorMessage}" });

                return Ok(new { Message = "Gửi email hóa đơn thành công!" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi hệ thống khi gửi email hóa đơn {HoaDonId}.", id);
                return StatusCode(500, new { Message = "Đã xảy ra lỗi hệ thống khi gửi email hóa đơn." });
            }
        }
    }
}

