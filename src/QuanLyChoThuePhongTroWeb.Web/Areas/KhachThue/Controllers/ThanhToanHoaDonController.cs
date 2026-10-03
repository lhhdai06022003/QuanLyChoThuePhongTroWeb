using System;
using System.IO;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using QuanLyChoThuePhongTroWeb.Application.Common.Configurations;
using QuanLyChoThuePhongTroWeb.Application.Common.Files;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Services;
using QuanLyChoThuePhongTroWeb.Web.Helpers;

namespace QuanLyChoThuePhongTroWeb.Areas.KhachThue.Controllers
{
    // Khung "Thanh toán" của khách thuê (spec §27.1). Quyền sở hữu hóa đơn do Application kiểm.
    [Area("KhachThue")]
    [Authorize(Roles = "KhachThue")]
    [AutoValidateAntiforgeryToken]
    public class ThanhToanHoaDonController : Controller
    {
        private const long MaxRequestBytes = 6 * 1024 * 1024;

        private readonly IInvoicePaymentRequestService _service;
        private readonly InvoicePaymentOptions _options;

        public ThanhToanHoaDonController(IInvoicePaymentRequestService service, InvoicePaymentOptions options)
        {
            _service = service;
            _options = options;
        }

        public sealed class TaoLuotReq
        {
            public int HoaDonId { get; set; }
            public decimal? SoTien { get; set; }
        }

        public sealed class NopMinhChungForm
        {
            public int YeuCauId { get; set; }
            public IFormFile? File { get; set; }
            public string? MaGiaoDich { get; set; }
            // Giờ Việt Nam từ ô datetime-local.
            public string? NgayChuyen { get; set; }
        }

        [HttpGet("/KhachThue/HoaDon/ThanhToan/{hoaDonId:int}")]
        public async Task<IActionResult> Panel(int hoaDonId, CancellationToken ct)
        {
            if (!TryGetTenant(out var nguoiThueId, out _))
            {
                return Unauthorized(new { success = false, message = "Tài khoản chưa liên kết hồ sơ người thuê." });
            }

            var result = await _service.GetPaymentPanelAsync(hoaDonId, nguoiThueId, ct);
            return result.Success ? Ok(new { success = true, data = result.Data }) : this.ToErrorResult(result);
        }

        [HttpPost("/KhachThue/HoaDon/ThanhToan/Tao")]
        public async Task<IActionResult> Tao([FromBody] TaoLuotReq req, CancellationToken ct)
        {
            if (!TryGetTenant(out var nguoiThueId, out var userId))
            {
                return Unauthorized(new { success = false, message = "Tài khoản chưa liên kết hồ sơ người thuê." });
            }

            var result = await _service.CreateAsync(req?.HoaDonId ?? 0, nguoiThueId, userId, req?.SoTien, ct);
            return result.Success ? Ok(new { success = true, message = result.Message, data = result.Data }) : this.ToErrorResult(result);
        }

        [HttpPost("/KhachThue/HoaDon/ThanhToan/Huy/{yeuCauId:int}")]
        public async Task<IActionResult> Huy(int yeuCauId, CancellationToken ct)
        {
            if (!TryGetTenant(out var nguoiThueId, out var userId))
            {
                return Unauthorized(new { success = false, message = "Tài khoản chưa liên kết hồ sơ người thuê." });
            }

            var result = await _service.CancelAsync(yeuCauId, nguoiThueId, userId, ct);
            return result.Success ? Ok(new { success = true, message = result.Message }) : this.ToErrorResult(result);
        }

        [HttpPost("/KhachThue/HoaDon/ThanhToan/NopMinhChung")]
        [RequestSizeLimit(MaxRequestBytes)]
        [RequestFormLimits(MultipartBodyLengthLimit = MaxRequestBytes)]
        public async Task<IActionResult> NopMinhChung([FromForm] NopMinhChungForm form, CancellationToken ct)
        {
            if (!TryGetTenant(out var nguoiThueId, out var userId))
            {
                return Unauthorized(new { success = false, message = "Tài khoản chưa liên kết hồ sơ người thuê." });
            }

            if (!VietnamTime.TryParseToUtc(form.NgayChuyen, out var ngayChuyenUtc))
            {
                return BadRequest(new { success = false, message = "Vui lòng nhập thời điểm chuyển khoản hợp lệ." });
            }

            if (form.File == null || form.File.Length == 0)
            {
                return BadRequest(new { success = false, message = "Vui lòng chọn ảnh minh chứng." });
            }

            if (form.File.Length > _options.MinhChungToiDaBytes)
            {
                return BadRequest(new { success = false, message = $"Ảnh vượt quá dung lượng cho phép ({_options.MinhChungToiDaBytes / (1024 * 1024)} MB)." });
            }

            await using var buffer = new MemoryStream();
            await form.File.CopyToAsync(buffer, ct);
            buffer.Position = 0;
            var upload = new UploadFile(buffer, Path.GetFileName(form.File.FileName), form.File.ContentType ?? string.Empty, buffer.Length);

            // Kiểm ở Web trước, Application kiểm lại (spec §12).
            var fileError = ProofImageValidator.Validate(upload, _options.MinhChungToiDaBytes);
            if (fileError != null)
            {
                return BadRequest(new { success = false, message = fileError });
            }

            var result = await _service.SubmitProofAsync(new SubmitPaymentProofRequest
            {
                YeuCauId = form.YeuCauId,
                File = upload,
                MaGiaoDichNganHang = form.MaGiaoDich,
                NgayChuyenUtc = ngayChuyenUtc
            }, nguoiThueId, userId, ct);

            return result.Success ? Ok(new { success = true, message = result.Message, data = result.Data }) : this.ToErrorResult(result);
        }

        [HttpGet("/KhachThue/HoaDon/ThanhToan/QR/{yeuCauId:int}")]
        public async Task<IActionResult> QR(int yeuCauId, CancellationToken ct)
        {
            if (!TryGetTenant(out var nguoiThueId, out _))
            {
                return Unauthorized();
            }

            var result = await _service.GetQrAsync(yeuCauId, nguoiThueId, ct);
            return result.Success ? File(result.Data!, "image/png") : this.ToErrorResult(result);
        }

        [HttpGet("/KhachThue/HoaDon/ThanhToan/AnhMinhChung/{minhChungId:int}")]
        public async Task<IActionResult> AnhMinhChung(int minhChungId, CancellationToken ct)
        {
            if (!TryGetTenant(out var nguoiThueId, out _))
            {
                return Unauthorized();
            }

            var result = await _service.GetProofImageAsync(minhChungId, nguoiThueId, ct);
            return result.Success ? File(result.Data!.Bytes, result.Data.ContentType) : this.ToErrorResult(result);
        }

        private bool TryGetTenant(out int nguoiThueId, out int userId)
        {
            userId = 0;
            return int.TryParse(User.FindFirst("NguoiThueId")?.Value, out nguoiThueId) && nguoiThueId > 0 &&
                   int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId) && userId > 0;
        }
    }
}
