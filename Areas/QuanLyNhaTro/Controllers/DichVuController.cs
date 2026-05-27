using Microsoft.AspNetCore.Mvc;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Services.DichVus;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Services.PhongTros;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels.Requests;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Controllers
{
    [Area("QuanLyNhaTro")]
    [ApiController]
    [Microsoft.AspNetCore.Authorization.Authorize]
    public class DichVuController : Controller
    {
        private readonly IDichVuService _dichVuService;
        private readonly IPhongTroService _phongTroService;

        public DichVuController(IDichVuService dichVuService, IPhongTroService phongTroService)
        {
            _dichVuService = dichVuService;
            _phongTroService = phongTroService;
        }

        [Route("QuanLyNhaTro/QuanLyDichVu")]
        [ApiExplorerSettings(IgnoreApi = true)]
        public async Task<IActionResult> Index()
        {
            ViewBag.ListChiNhanh = await _phongTroService.GetDanhSachChiNhanhDropdownAsync();
            return View();
        }

        // --- CRUD Dich Vu System ---
        [HttpPost("/DichVu/GetList")]
        public async Task<IActionResult> GetListDichVu()
        {
            var form = Request.Form;
            var request = new DataTableRequest
            {
                Draw = int.TryParse(form["draw"].FirstOrDefault(), out int d) ? d : 1,
                Start = int.TryParse(form["start"].FirstOrDefault(), out int s) ? s : 0,
                Length = int.TryParse(form["length"].FirstOrDefault(), out int l) ? l : 10,
                SearchValue = form["search[value]"].FirstOrDefault(),
                SortDirection = form["order[0][dir]"].FirstOrDefault() ?? "desc"
            };
            int sortColumnIndex = int.TryParse(form["order[0][column]"].FirstOrDefault(), out int colIdx) ? colIdx : 0;
            request.SortColumnName = form[$"columns[{sortColumnIndex}][data]"].FirstOrDefault();

            var data = await _dichVuService.GetDanhSachDichVuAsync(request);
            return Ok(data);
        }

        [HttpGet("/DichVu/GetById/{id}")]
        public async Task<IActionResult> GetDichVuById(int id)
        {
            var data = await _dichVuService.GetDichVuByIdAsync(id);
            if (data == null) return NotFound(new { Message = "Không tìm thấy" });
            return Ok(data);
        }

        [HttpPost("/DichVu/Create")]
        public async Task<IActionResult> CreateDichVu([FromBody] DichVuReq request)
        {
            var result = await _dichVuService.CreateDichVuAsync(request);
            if (!result.IsSuccess) return BadRequest(new { Message = result.ErrorMessage });
            return Ok(new { Message = "Thêm thành công!" });
        }

        [HttpPut("/DichVu/Update/{id}")]
        public async Task<IActionResult> UpdateDichVu(int id, [FromBody] DichVuReq request)
        {
            var result = await _dichVuService.UpdateDichVuAsync(id, request);
            if (!result.IsSuccess) return BadRequest(new { Message = result.ErrorMessage });
            return Ok(new { Message = "Cập nhật thành công!" });
        }

        [HttpDelete("/DichVu/Delete/{id}")]
        public async Task<IActionResult> DeleteDichVu(int id)
        {
            var result = await _dichVuService.DeleteDichVuAsync(id);
            if (!result.IsSuccess) return BadRequest(new { Message = result.ErrorMessage });
            return Ok(new { Message = "Đã xóa thành công." });
        }

        // --- CRUD Dich Vu Chi Nhanh (Bang Gia) ---
        [HttpPost("/DichVuChiNhanh/GetList")]
        public async Task<IActionResult> GetListDichVuChiNhanh([FromForm] int? chiNhanhId)
        {
            // Đảm bảo có giá trị mặc định nếu JS chưa truyền lên kịp
            int validChiNhanhId = chiNhanhId ?? 0;

            var form = Request.Form;
            var request = new DataTableRequest
            {
                Draw = int.TryParse(form["draw"].FirstOrDefault(), out int d) ? d : 1,
                Start = int.TryParse(form["start"].FirstOrDefault(), out int s) ? s : 0,
                Length = int.TryParse(form["length"].FirstOrDefault(), out int l) ? l : 10,
                SearchValue = form["search[value]"].FirstOrDefault()
            };

            var data = await _dichVuService.GetDanhSachDichVuChiNhanhAsync(request, validChiNhanhId);
            return Ok(data);
        }

        [HttpGet("/DichVuChiNhanh/GetById/{id}")]
        public async Task<IActionResult> GetDichVuChiNhanhById(int id)
        {
            var data = await _dichVuService.GetDichVuChiNhanhByIdAsync(id);
            if (data == null) return NotFound(new { Message = "Không tìm thấy" });
            return Ok(data);
        }

        [HttpPost("/DichVuChiNhanh/Create")]
        public async Task<IActionResult> CreateDichVuChiNhanh([FromBody] DichVuChiNhanhReq request)
        {
            var result = await _dichVuService.CreateDichVuChiNhanhAsync(request);
            if (!result.IsSuccess) return BadRequest(new { Message = result.ErrorMessage });
            return Ok(new { Message = "Thêm bảng giá thành công!" });
        }

        [HttpPut("/DichVuChiNhanh/Update/{id}")]
        public async Task<IActionResult> UpdateDichVuChiNhanh(int id, [FromBody] DichVuChiNhanhReq request)
        {
            var result = await _dichVuService.UpdateDichVuChiNhanhAsync(id, request);
            if (!result.IsSuccess) return BadRequest(new { Message = result.ErrorMessage });
            return Ok(new { Message = "Cập nhật bảng giá thành công!" });
        }

        [HttpDelete("/DichVuChiNhanh/Delete/{id}")]
        public async Task<IActionResult> DeleteDichVuChiNhanh(int id)
        {
            var result = await _dichVuService.DeleteDichVuChiNhanhAsync(id);
            if (!result.IsSuccess) return BadRequest(new { Message = result.ErrorMessage });
            return Ok(new { Message = "Đã xóa bảng giá thành công." });
        }

        // Endpoint phụ để lấy dropdown dịch vụ hệ thống cho bảng giá
        [HttpGet("/DichVu/GetDropdown")]
        public async Task<IActionResult> GetDichVuDropdown()
        {
            var req = new DataTableRequest { Start = 0, Length = 1000 };
            var data = await _dichVuService.GetDanhSachDichVuAsync(req);
            return Ok(data.data.Select(x => new { value = x.DichVuId, text = $"{x.TenDichVu} ({x.DonVi})" }));
        }
    }
}
