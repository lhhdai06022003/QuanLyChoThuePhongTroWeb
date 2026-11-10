using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels;
using System;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Payments;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Services;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels.Responses;
using QuanLyChoThuePhongTroWeb.Web.Helpers;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Controllers
{
    [ApiController]
    public class LichSuThanhToanController : AdminBaseController
    {
        private readonly ILichSuThanhToanService _lichSuThanhToanService;
        private readonly IPhongTroService _phongTroService;
        private readonly IInvoiceLedgerService _ledgerService;
        private readonly INguoiDungService _nguoiDungService;
        private readonly Microsoft.Extensions.Configuration.IConfiguration _configuration;

        public LichSuThanhToanController(
            ILichSuThanhToanService lichSuThanhToanService,
            IPhongTroService phongTroService,
            IInvoiceLedgerService ledgerService,
            INguoiDungService nguoiDungService,
            Microsoft.Extensions.Configuration.IConfiguration configuration)
        {
            _lichSuThanhToanService = lichSuThanhToanService;
            _phongTroService = phongTroService;
            _ledgerService = ledgerService;
            _nguoiDungService = nguoiDungService;
            _configuration = configuration;
        }

        public class HuyGhiNhanReq
        {
            public string? LyDo { get; set; }
        }

        // Các tham số lọc nhận dạng chuỗi và parse mềm: giá trị sai thì bỏ qua, trang vẫn mở bình thường.
        [Route("QuanLyNhaTro/LichSuThanhToan")]
        [ApiExplorerSettings(IgnoreApi = true)]
        public async Task<IActionResult> Index(string? kyThang, string? kyNam, string? nguoiThu, string? phuongThuc, string? tuNgay, string? denNgay, string? chiNhanhId)
        {
            var laAdmin = User.IsInRole("Admin");
            var chiNhanhs = await _phongTroService.GetDanhSachChiNhanhDropdownAsync(CurrentActorId);

            var chonChiNhanh = 0;
            if (int.TryParse(chiNhanhId, NumberStyles.None, CultureInfo.InvariantCulture, out var cn)
                && chiNhanhs.Any(c => c.Value == cn.ToString(CultureInfo.InvariantCulture)))
            {
                chonChiNhanh = cn;
            }

            var slugKenh = KenhThuSlugs.TryParse(phuongThuc, out var kenh) ? KenhThuSlugs.Slug(kenh) : "";

            // Kỳ hóa đơn chỉ dành cho Admin (máy chủ cũng bỏ qua với nhân viên).
            int? ky1 = null, ky2 = null;
            if (laAdmin
                && int.TryParse(kyThang, NumberStyles.None, CultureInfo.InvariantCulture, out var m) && m is >= 1 and <= 12
                && int.TryParse(kyNam, NumberStyles.None, CultureInfo.InvariantCulture, out var y) && y is >= 2000 and <= 2100)
            {
                ky1 = m;
                ky2 = y;
            }

            var tu = ParseNgayIso(tuNgay);
            var den = ParseNgayIso(denNgay);
            var coKhoang = tu.HasValue && den.HasValue && tu.Value <= den.Value;

            var (nguoiThuChon, tenNguoiThu) = await ChonNguoiThuAsync(nguoiThu, laAdmin);

            var model = LichSuThanhToanPageViewModel.From(
                chiNhanhs, laAdmin, chonChiNhanh, slugKenh, nguoiThuChon, tenNguoiThu, ky1, ky2,
                coKhoang ? tu!.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : "",
                coKhoang ? den!.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : "",
                _configuration["VietQRSettings:BankId"] ?? "MB",
                _configuration["VietQRSettings:AccountNumber"] ?? "",
                _configuration["VietQRSettings:AccountName"] ?? "");
            return View(model);
        }

        // "", "toi", "he-thong" ai cũng dùng được; NguoiDungId chỉ Admin (nhân viên gửi số thì về "tất cả").
        private async Task<(string Chon, string? Ten)> ChonNguoiThuAsync(string? nguoiThu, bool laAdmin)
        {
            var s = nguoiThu?.Trim().ToLowerInvariant();
            if (s == "toi" || s == "he-thong")
            {
                return (s, null);
            }

            if (laAdmin && int.TryParse(s, NumberStyles.None, CultureInfo.InvariantCulture, out var id) && id > 0)
            {
                var user = await _nguoiDungService.GetByIdAsync(id);
                return (id.ToString(CultureInfo.InvariantCulture), user?.TenDangNhap ?? $"Người dùng #{id}");
            }

            return ("", null);
        }

        private static DateTime? ParseNgayIso(string? text)
        {
            return DateTime.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var ngay) ? ngay.Date : null;
        }

        [HttpPost("/LichSuThanhToan/GetList")]
        public async Task<IActionResult> GetList(
            [FromForm] int chiNhanhId = 0, 
            [FromForm] string? phuongThuc = null,
            [FromForm] string dateRange = "",
            [FromForm] bool coTienThua = false,
            [FromForm] string? nguoiThu = null,
            [FromForm] int? kyThang = null,
            [FromForm] int? kyNam = null)
        {
            if (!TryGetActorId(out var actorId))
            {
                return Unauthorized(new { Message = "Người dùng chưa đăng nhập hoặc phiên làm việc đã hết hạn." });
            }

            var form = Request.Form;
            var request = new DataTableRequest
            {
                Draw = int.TryParse(form["draw"].FirstOrDefault(), out int d) ? d : 1,
                Start = int.TryParse(form["start"].FirstOrDefault(), out int s) ? s : 0,
                Length = int.TryParse(form["length"].FirstOrDefault(), out int l) ? l : 10,
                SearchValue = form["search[value]"].FirstOrDefault()
            };

            var (tuNgay, denNgay) = TachKhoangNgay(dateRange);

            var loc = new LichSuThanhToanLocReq
            {
                ChiNhanhId = chiNhanhId,
                PhuongThuc = phuongThuc,
                TuNgay = tuNgay,
                DenNgay = denNgay,
                ChiCoTienThua = coTienThua,
                NguoiThu = nguoiThu,
                KyThang = kyThang,
                KyNam = kyNam
            };

            var data = await _lichSuThanhToanService.GetDanhSachThanhToanAsync(actorId, request, loc);
            return Ok(data);
        }

        [HttpGet("/LichSuThanhToan/GetStats")]
        public async Task<IActionResult> GetStats(
            [FromQuery] int chiNhanhId = 0,
            [FromQuery] string dateRange = "",
            [FromQuery] string? nguoiThu = null,
            [FromQuery] int? kyThang = null,
            [FromQuery] int? kyNam = null)
        {
            if (!TryGetActorId(out var actorId))
            {
                return Unauthorized(new { Message = "Người dùng chưa đăng nhập hoặc phiên làm việc đã hết hạn." });
            }

            var (tuNgay, denNgay) = TachKhoangNgay(dateRange);

            var stats = await _lichSuThanhToanService.GetThongKeThanhToanAsync(actorId, new LichSuThanhToanLocReq
            {
                ChiNhanhId = chiNhanhId,
                TuNgay = tuNgay,
                DenNgay = denNgay,
                NguoiThu = nguoiThu,
                KyThang = kyThang,
                KyNam = kyNam
            });
            return Ok(stats);
        }

        // Hủy ghi nhận thanh toán: chỉ Admin, bắt buộc lý do; không hoàn tiền qua ngân hàng (spec thanh toán §20).
        [HttpDelete("/LichSuThanhToan/HuyThanhToan/{id}")]
        public async Task<IActionResult> HuyThanhToan(int id, [FromBody] HuyGhiNhanReq req, CancellationToken ct)
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) || userId <= 0)
            {
                return Unauthorized(new { Message = "Người dùng chưa đăng nhập hoặc phiên làm việc đã hết hạn." });
            }

            var result = await _ledgerService.VoidPaymentAsync(id, userId, req?.LyDo ?? string.Empty, ct);
            if (!result.Success)
            {
                return this.ToErrorResult(result);
            }

            return Ok(new { Message = result.Message, data = result.Data });
        }

        // dateRange là chuỗi của flatpickr dạng "yyyy-MM-dd {dấu ngăn} yyyy-MM-dd" hoặc "yyyy-MM-dd".
        // Dấu ngăn mặc định là " to ", còn locale vn (l10n/vn.js 4.6.13) dùng " đến ", nên chấp nhận cả hai.
        // Kết quả là ngày lịch Việt Nam; store đổi sang mốc UTC.
        private static (DateTime? TuNgay, DateTime? DenNgay) TachKhoangNgay(string? dateRange)
        {
            if (string.IsNullOrWhiteSpace(dateRange))
            {
                return (null, null);
            }

            var parts = dateRange.Split(new[] { " to ", " đến " }, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2)
            {
                return (ParseNgay(parts[0]), ParseNgay(parts[1]));
            }

            if (parts.Length == 1)
            {
                var ngay = ParseNgay(parts[0]);
                return (ngay, ngay);
            }

            return (null, null);
        }

        private static DateTime? ParseNgay(string text)
        {
            return DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var ngay) ? ngay.Date : null;
        }

        private bool TryGetActorId(out int actorId)
        {
            return int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out actorId) && actorId > 0;
        }
    }
}

