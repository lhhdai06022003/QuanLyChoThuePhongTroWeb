using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Security;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Services;
using QuanLyChoThuePhongTroWeb.Web.Helpers;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Controllers
{
    // Màn "Đối chiếu thanh toán" (spec thanh toán §27.2). Quyền chi nhánh do Application kiểm.
    [ApiController]
    public class DoiChieuThanhToanController : AdminBaseController
    {
        private readonly IInvoicePaymentConfirmationService _service;
        private readonly IPhongTroService _phongTroService;
        private readonly IEmployeeAccessService _employeeAccessService;

        public DoiChieuThanhToanController(
            IInvoicePaymentConfirmationService service,
            IPhongTroService phongTroService,
            IEmployeeAccessService employeeAccessService)
        {
            _service = service;
            _phongTroService = phongTroService;
            _employeeAccessService = employeeAccessService;
        }

        public class XacNhanReq
        {
            public int MinhChungId { get; set; }
            public decimal SoTienThucNhan { get; set; }
            public string? MaGiaoDich { get; set; }
            // Giờ Việt Nam, ví dụ "2026-10-03T14:30".
            public string? NgayGiaoDich { get; set; }
            public string? GhiChu { get; set; }
        }

        public class TuChoiReq
        {
            public int MinhChungId { get; set; }
            public string? LyDo { get; set; }
        }

        [Route("QuanLyNhaTro/DoiChieuThanhToan")]
        [ApiExplorerSettings(IgnoreApi = true)]
        public async Task<IActionResult> Index([FromQuery] string? ma)
        {
            ViewBag.ListChiNhanh = await _phongTroService.GetDanhSachChiNhanhDropdownAsync(CurrentActorId);
            ViewBag.MaTraCuu = ma ?? string.Empty;

            var chuaPhanCong = false;
            if (User.IsInRole("NhanVien") && TryGetActorId(out var actorId))
            {
                var scope = await _employeeAccessService.GetScopeAsync(actorId);
                chuaPhanCong = scope != null && !scope.IsAdmin && scope.ActiveBranchIds.Count == 0;
            }

            ViewBag.ChuaPhanCongChiNhanh = chuaPhanCong;
            return View();
        }

        [HttpPost("/DoiChieuThanhToan/DanhSach")]
        public async Task<IActionResult> DanhSach(
            [FromForm] int chiNhanhId = 0,
            [FromForm] bool daXuLy = false,
            [FromForm] bool chiChoAdmin = false,
            CancellationToken ct = default)
        {
            if (!TryGetActorId(out var actorId))
                return Unauthorized(new { message = "Người dùng chưa đăng nhập hoặc phiên làm việc đã hết hạn." });

            var form = Request.Form;
            var request = new DataTableRequest
            {
                Draw = int.TryParse(form["draw"].FirstOrDefault(), out var d) ? d : 1,
                Start = int.TryParse(form["start"].FirstOrDefault(), out var s) ? s : 0,
                Length = int.TryParse(form["length"].FirstOrDefault(), out var l) ? l : 25,
                SearchValue = form["search[value]"].FirstOrDefault()
            };

            var result = await _service.GetReviewQueueAsync(actorId,
                new ReviewQueueFilter { ChiNhanhId = chiNhanhId, DaXuLy = daXuLy, ChiChoAdmin = chiChoAdmin }, request, ct);
            return result.Success ? Ok(result.Data) : this.ToErrorResult(result);
        }

        [HttpGet("/DoiChieuThanhToan/ChiTiet/{minhChungId:int}")]
        public async Task<IActionResult> ChiTiet(int minhChungId, CancellationToken ct)
        {
            if (!TryGetActorId(out var actorId))
                return Unauthorized(new { message = "Người dùng chưa đăng nhập hoặc phiên làm việc đã hết hạn." });

            var result = await _service.GetProofDetailAsync(minhChungId, actorId, ct);
            return result.Success ? Ok(new { success = true, data = result.Data }) : this.ToErrorResult(result);
        }

        [HttpGet("/DoiChieuThanhToan/AnhMinhChung/{minhChungId:int}")]
        [ApiExplorerSettings(IgnoreApi = true)]
        public async Task<IActionResult> AnhMinhChung(int minhChungId, CancellationToken ct)
        {
            if (!TryGetActorId(out var actorId))
                return Unauthorized();

            var result = await _service.GetProofImageAsync(minhChungId, actorId, ct);
            return result.Success ? File(result.Data!.Bytes, result.Data.ContentType) : this.ToErrorResult(result);
        }

        [HttpPost("/DoiChieuThanhToan/XacNhan")]
        public async Task<IActionResult> XacNhan([FromBody] XacNhanReq req, CancellationToken ct)
        {
            if (!TryGetActorId(out var actorId))
                return Unauthorized(new { message = "Người dùng chưa đăng nhập hoặc phiên làm việc đã hết hạn." });

            if (req == null || !VietnamTime.TryParseToUtc(req.NgayGiaoDich, out var ngayGiaoDichUtc))
                return BadRequest(new { success = false, message = "Vui lòng nhập ngày giao dịch hợp lệ." });

            var result = await _service.ConfirmAsync(new ConfirmPaymentRequest
            {
                MinhChungId = req.MinhChungId,
                SoTienThucNhan = req.SoTienThucNhan,
                MaGiaoDich = req.MaGiaoDich,
                NgayGiaoDichUtc = ngayGiaoDichUtc,
                GhiChu = req.GhiChu
            }, actorId, ct);

            return result.Success
                ? Ok(new { success = true, message = result.Data!.Message, data = result.Data })
                : this.ToErrorResult(result);
        }

        [HttpPost("/DoiChieuThanhToan/TuChoi")]
        public async Task<IActionResult> TuChoi([FromBody] TuChoiReq req, CancellationToken ct)
        {
            if (!TryGetActorId(out var actorId))
                return Unauthorized(new { message = "Người dùng chưa đăng nhập hoặc phiên làm việc đã hết hạn." });

            var result = await _service.RejectAsync(req?.MinhChungId ?? 0, actorId, req?.LyDo ?? string.Empty, ct);
            return result.Success ? Ok(new { success = true, message = result.Message }) : this.ToErrorResult(result);
        }

        [HttpGet("/DoiChieuThanhToan/TraCuu")]
        public async Task<IActionResult> TraCuu([FromQuery] string? ma, CancellationToken ct)
        {
            if (!TryGetActorId(out var actorId))
                return Unauthorized(new { message = "Người dùng chưa đăng nhập hoặc phiên làm việc đã hết hạn." });

            var result = await _service.SearchPaymentRequestAsync(ma ?? string.Empty, actorId, ct);
            return result.Success ? Ok(new { success = true, data = result.Data }) : this.ToErrorResult(result);
        }

        private bool TryGetActorId(out int actorId)
        {
            return int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out actorId) && actorId > 0;
        }
    }
}
