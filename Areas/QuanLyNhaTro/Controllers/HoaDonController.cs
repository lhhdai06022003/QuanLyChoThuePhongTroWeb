using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Services.HoaDons;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Services.PhongTros;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels.Requests;
using System.Security.Claims;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Controllers
{
    [Area("QuanLyNhaTro")]
    [ApiController]
    [Authorize]
    public class HoaDonController : Controller
    {
        private readonly IHoaDonService _hoaDonService;
        private readonly IPhongTroService _phongTroService;

        public HoaDonController(IHoaDonService hoaDonService, IPhongTroService phongTroService)
        {
            _hoaDonService = hoaDonService;
            _phongTroService = phongTroService;
        }

        [Route("QuanLyNhaTro/QuanLyHoaDon")]
        [ApiExplorerSettings(IgnoreApi = true)]
        public async Task<IActionResult> Index()
        {
            ViewBag.ListChiNhanh = await _phongTroService.GetDanhSachChiNhanhDropdownAsync();
            return View();
        }

        // Phát sinh hóa đơn hàng loạt
        [HttpPost("/HoaDon/PhatSinh")]
        public async Task<IActionResult> PhatSinh([FromBody] PhatSinhHoaDonReq req)
        {
            if (req.ChiNhanhId <= 0 || req.Thang < 1 || req.Thang > 12 || req.Nam < 2000)
                return BadRequest(new { Message = "Tham số không hợp lệ." });

            var result = await _hoaDonService.PhatSinhHoaDonAsync(req.ChiNhanhId, req.Thang, req.Nam);
            if (!result.IsSuccess)
                return BadRequest(new { Message = result.Message });

            return Ok(new { Message = result.Message, SoHoaDonMoi = result.SoHoaDonMoi });
        }

        // Danh sách hóa đơn (DataTable server-side)
        [HttpPost("/HoaDon/GetList")]
        public async Task<IActionResult> GetList([FromQuery] int chiNhanhId = 0, [FromQuery] int thang = 0, [FromQuery] int nam = 0, [FromQuery] int trangThai = -1)
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
    }
}
