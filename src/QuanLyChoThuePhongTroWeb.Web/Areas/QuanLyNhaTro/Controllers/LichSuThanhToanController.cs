using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Controllers
{
    [ApiController]
    public class LichSuThanhToanController : AdminBaseController
    {
        private readonly ILichSuThanhToanService _lichSuThanhToanService;
        private readonly IPhongTroService _phongTroService;

        public LichSuThanhToanController(ILichSuThanhToanService lichSuThanhToanService, IPhongTroService phongTroService)
        {
            _lichSuThanhToanService = lichSuThanhToanService;
            _phongTroService = phongTroService;
        }

        [Route("QuanLyNhaTro/LichSuThanhToan")]
        [ApiExplorerSettings(IgnoreApi = true)]
        public async Task<IActionResult> Index()
        {
            ViewBag.ListChiNhanh = await _phongTroService.GetDanhSachChiNhanhDropdownAsync();
            return View();
        }

        [HttpPost("/LichSuThanhToan/GetList")]
        public async Task<IActionResult> GetList(
            [FromForm] int chiNhanhId = 0, 
            [FromForm] int phuongThuc = -1, 
            [FromForm] string dateRange = "")
        {
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

            var data = await _lichSuThanhToanService.GetDanhSachThanhToanAsync(request, chiNhanhId, phuongThuc, tuNgay, denNgay);
            return Ok(data);
        }

        [HttpGet("/LichSuThanhToan/GetStats")]
        public async Task<IActionResult> GetStats([FromQuery] int chiNhanhId = 0, [FromQuery] string dateRange = "")
        {
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

            var stats = await _lichSuThanhToanService.GetThongKeThanhToanAsync(chiNhanhId, tuNgay, denNgay);
            return Ok(stats);
        }

        [HttpDelete("/LichSuThanhToan/HuyThanhToan/{id}")]
        public async Task<IActionResult> HuyThanhToan(int id)
        {
            int userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var result = await _lichSuThanhToanService.HuyGiaoDichAsync(id, userId);

            if (!result.IsSuccess)
            {
                return BadRequest(new { Message = result.ErrorMessage });
            }

            return Ok(new { Message = "Đã hủy giao dịch thanh toán thành công. Trạng thái hóa đơn đã được khôi phục về Chưa thanh toán." });
        }
    }
}

