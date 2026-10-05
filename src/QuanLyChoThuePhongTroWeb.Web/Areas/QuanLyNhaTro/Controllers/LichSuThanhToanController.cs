using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels;
using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Services;
using QuanLyChoThuePhongTroWeb.Web.Helpers;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Controllers
{
    [ApiController]
    public class LichSuThanhToanController : AdminBaseController
    {
        private readonly ILichSuThanhToanService _lichSuThanhToanService;
        private readonly IPhongTroService _phongTroService;
        private readonly IInvoiceLedgerService _ledgerService;

        public LichSuThanhToanController(
            ILichSuThanhToanService lichSuThanhToanService,
            IPhongTroService phongTroService,
            IInvoiceLedgerService ledgerService)
        {
            _lichSuThanhToanService = lichSuThanhToanService;
            _phongTroService = phongTroService;
            _ledgerService = ledgerService;
        }

        public class HuyGhiNhanReq
        {
            public string? LyDo { get; set; }
        }

        [Route("QuanLyNhaTro/LichSuThanhToan")]
        [ApiExplorerSettings(IgnoreApi = true)]
        public async Task<IActionResult> Index()
        {
            ViewBag.ListChiNhanh = await _phongTroService.GetDanhSachChiNhanhDropdownAsync(CurrentActorId);
            return View();
        }

        [HttpPost("/LichSuThanhToan/GetList")]
        public async Task<IActionResult> GetList(
            [FromForm] int chiNhanhId = 0, 
            [FromForm] int phuongThuc = -1, 
            [FromForm] string dateRange = "",
            [FromForm] bool coTienThua = false)
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

            DateTime? tuNgay = null;
            DateTime? denNgay = null;

            if (!string.IsNullOrEmpty(dateRange))
            {
                // dateRange format: "yyyy-MM-dd to yyyy-MM-dd" hoặc "yyyy-MM-dd"
                var parts = dateRange.Split(" to ");
                if (parts.Length == 2)
                {
                    if (DateTime.TryParse(parts[0], out DateTime t)) tuNgay = t;
                    if (DateTime.TryParse(parts[1], out DateTime dn)) denNgay = dn;
                }
                else if (parts.Length == 1)
                {
                    if (DateTime.TryParse(parts[0], out DateTime t))
                    {
                        tuNgay = t;
                        denNgay = t;
                    }
                }
            }

            var data = await _lichSuThanhToanService.GetDanhSachThanhToanAsync(actorId, request, chiNhanhId, phuongThuc, tuNgay, denNgay, coTienThua);
            return Ok(data);
        }

        [HttpGet("/LichSuThanhToan/GetStats")]
        public async Task<IActionResult> GetStats([FromQuery] int chiNhanhId = 0, [FromQuery] string dateRange = "")
        {
            if (!TryGetActorId(out var actorId))
            {
                return Unauthorized(new { Message = "Người dùng chưa đăng nhập hoặc phiên làm việc đã hết hạn." });
            }

            DateTime? tuNgay = null;
            DateTime? denNgay = null;

            if (!string.IsNullOrEmpty(dateRange))
            {
                var parts = dateRange.Split(" to ");
                if (parts.Length == 2)
                {
                    if (DateTime.TryParse(parts[0], out DateTime t)) tuNgay = t;
                    if (DateTime.TryParse(parts[1], out DateTime dn)) denNgay = dn;
                }
                else if (parts.Length == 1)
                {
                    if (DateTime.TryParse(parts[0], out DateTime t))
                    {
                        tuNgay = t;
                        denNgay = t;
                    }
                }
            }

            var stats = await _lichSuThanhToanService.GetThongKeThanhToanAsync(actorId, chiNhanhId, tuNgay, denNgay);
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

        private bool TryGetActorId(out int actorId)
        {
            return int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out actorId) && actorId > 0;
        }
    }
}

