using Microsoft.AspNetCore.Mvc;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Services.HopDongs;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Services.NguoiThues;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Services.PhongTros;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels.Requests;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Controllers
{
    [Area("QuanLyNhaTro")]
    [ApiController]
    [Microsoft.AspNetCore.Authorization.Authorize]
    [AutoValidateAntiforgeryToken]
    public class HopDongController : Controller
    {
        private readonly IHopDongService _hopDongService;
        private readonly IPhongTroService _phongTroService;
        private readonly INguoiThueService _nguoiThueService;
        private readonly QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Services.DieuKhoanMaus.IDieuKhoanMauService _dieuKhoanMauService;

        public HopDongController(IHopDongService hopDongService, IPhongTroService phongTroService, INguoiThueService nguoiThueService, QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Services.DieuKhoanMaus.IDieuKhoanMauService dieuKhoanMauService)
        {
            _hopDongService = hopDongService;
            _phongTroService = phongTroService;
            _nguoiThueService = nguoiThueService;
            _dieuKhoanMauService = dieuKhoanMauService;
        }

        [Route("QuanLyNhaTro/QuanLyHopDong")]
        [ApiExplorerSettings(IgnoreApi = true)]
        public async Task<IActionResult> QuanLyHopDong()
        {
             ViewBag.ListChiNhanh = await _phongTroService.GetDanhSachChiNhanhDropdownAsync();
             ViewBag.ListPhongTro = await _phongTroService.DanhSachPhongTroConTrong(); 
             ViewBag.ListNguoiThue = await _nguoiThueService.DanhSachNguoiThue();
             ViewBag.ListDieuKhoanMau = await _dieuKhoanMauService.GetAllAsync();
            return View(); // Trả về view Index của chức năng quản lý hợp đồng
        }

        [HttpPost("/HopDong/DanhSachHopDongSideAsync")]
        public async Task<IActionResult> DanhSachHopDongSideAsync([FromForm] HopDongFilterReq request)
        {
            var form = Request.Form;

            // Nhóm 1: Trích xuất các tham số điều khiển cấu trúc Table
            var tableParams = new DataTableRequest
            {
                Draw = int.TryParse(form["draw"].FirstOrDefault(), out int d) ? d : 1,
                Start = int.TryParse(form["start"].FirstOrDefault(), out int s) ? s : 0,
                Length = int.TryParse(form["length"].FirstOrDefault(), out int l) ? l : 10,
                SearchValue = form["search[value]"].FirstOrDefault(),
                SortDirection = form["order[0][dir]"].FirstOrDefault() ?? "desc"
            };
            int sortColumnIndex = int.TryParse(form["order[0][column]"].FirstOrDefault(), out int colIdx) ? colIdx : 0;
            tableParams.SortColumnName = form[$"columns[{sortColumnIndex}][data]"].FirstOrDefault();
            
            request.Draw = tableParams.Draw;
            request.Start = tableParams.Start;
            request.Length = tableParams.Length;
            request.SearchValue = tableParams.SearchValue;
            request.SortDirection = tableParams.SortDirection;
            request.SortColumnName = tableParams.SortColumnName;

            var data = await _hopDongService.DanhSachHopDongSideAsync(request);
            return Ok(data);
        }

        [HttpGet("/HopDong/GetById/{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var data = await _hopDongService.GetByIdAsync(id);
            if (data == null) return NotFound(new { message = "Không tìm thấy hợp đồng." });
            return Ok(data);
        }

        [HttpPost("/HopDong/Create")]
        public async Task<IActionResult> Create([FromBody] HopDongReq request)
        {
            // ModelState đã được tự động kiểm tra nhờ [ApiController]
            var result = await _hopDongService.CreateAsync(request);
            if (!result.IsSuccess)
            {
                return BadRequest(new { Message = result.ErrorMessage });
            }
            return Ok(new { Message = "Thêm mới hợp đồng thành công!" });
        }

        [HttpPut("/HopDong/Update/{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] HopDongReq request)
        {
            if (id != request.HopDongId) return BadRequest(new { Message = "ID không khớp." });

            var result = await _hopDongService.UpdateAsync(id, request);
            if (!result.IsSuccess)
            {
                return BadRequest(new { Message = result.ErrorMessage });
            }
            return Ok(new { Message = "Cập nhật hợp đồng thành công!" });
        }

        [HttpDelete("/HopDong/Delete/{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var result = await _hopDongService.DeleteAsync(id);
            if (!result.IsSuccess)
            {
                return BadRequest(new { Message = result.ErrorMessage });
            }
            return Ok(new { Message = "Đã xóa hợp đồng thành công." });
        }

        [HttpGet("/HopDong/Print/{id}")]
        public async Task<IActionResult> Print(int id)
        {
            var data = await _hopDongService.GetPrintDataAsync(id);
            if (data == null) return NotFound(new { message = "Không tìm thấy hợp đồng." });
            return View(data);
        }

        [HttpGet("/HopDong/DownloadWord/{id}")]
        public async Task<IActionResult> DownloadWord(int id)
        {
            var data = await _hopDongService.GetPrintDataAsync(id);
            if (data == null) return NotFound(new { message = "Không tìm thấy hợp đồng." });

            try
            {
                var fileBytes = QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Services.HopDongs.WordExportService.GenerateHopDongWord(data);
                var fileName = $"HopDong_{data.MaHopDong}_{DateTime.Now:yyyyMMdd}.docx";
                return File(fileBytes, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", fileName);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = "Lỗi khi tạo file Word: " + ex.Message });
            }
        }
    }
}

