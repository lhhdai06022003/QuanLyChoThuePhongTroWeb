using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuanLyChoThuePhongTroWeb.Web.Helpers;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Controllers
{
    [ApiController]
    public class DichVuController : AdminBaseController
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
            ViewBag.ListChiNhanh = await _phongTroService.GetDanhSachChiNhanhDropdownAsync(CurrentActorId);
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

        // Danh mục dịch vụ hệ thống dùng chung cho mọi chi nhánh: chỉ Admin được sửa.
        [HttpPost("/DichVu/Create")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateDichVu([FromBody] DichVuReq request)
        {
            var result = await _dichVuService.CreateDichVuAsync(CurrentActorId, request);
            return result.Success ? Ok(new { Message = "Thêm thành công!" }) : this.ToErrorResult(result);
        }

        [HttpPut("/DichVu/Update/{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateDichVu(int id, [FromBody] DichVuReq request)
        {
            var result = await _dichVuService.UpdateDichVuAsync(CurrentActorId, id, request);
            return result.Success ? Ok(new { Message = "Cập nhật thành công!" }) : this.ToErrorResult(result);
        }

        [HttpDelete("/DichVu/Delete/{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteDichVu(int id)
        {
            var result = await _dichVuService.DeleteDichVuAsync(CurrentActorId, id);
            return result.Success ? Ok(new { Message = "Đã xóa thành công." }) : this.ToErrorResult(result);
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

            var data = await _dichVuService.GetDanhSachDichVuChiNhanhAsync(CurrentActorId, request, validChiNhanhId);
            return Ok(data);
        }

        [HttpGet("/DichVuChiNhanh/GetById/{id}")]
        public async Task<IActionResult> GetDichVuChiNhanhById(int id)
        {
            var data = await _dichVuService.GetDichVuChiNhanhByIdAsync(CurrentActorId, id);
            if (data == null) return NotFound(new { Message = "Không tìm thấy" });
            return Ok(data);
        }

        [HttpPost("/DichVuChiNhanh/Create")]
        public async Task<IActionResult> CreateDichVuChiNhanh([FromBody] DichVuChiNhanhReq request)
        {
            var result = await _dichVuService.CreateDichVuChiNhanhAsync(CurrentActorId, request);
            return result.Success ? Ok(new { Message = "Thêm bảng giá thành công!" }) : this.ToErrorResult(result);
        }

        [HttpPut("/DichVuChiNhanh/Update/{id}")]
        public async Task<IActionResult> UpdateDichVuChiNhanh(int id, [FromBody] DichVuChiNhanhReq request)
        {
            var result = await _dichVuService.UpdateDichVuChiNhanhAsync(CurrentActorId, id, request);
            return result.Success ? Ok(new { Message = "Cập nhật bảng giá thành công!" }) : this.ToErrorResult(result);
        }

        [HttpDelete("/DichVuChiNhanh/Delete/{id}")]
        public async Task<IActionResult> DeleteDichVuChiNhanh(int id)
        {
            var result = await _dichVuService.DeleteDichVuChiNhanhAsync(CurrentActorId, id);
            return result.Success ? Ok(new { Message = "Đã xóa bảng giá thành công." }) : this.ToErrorResult(result);
        }

        // Endpoint phụ để lấy dropdown dịch vụ hệ thống cho bảng giá
        [HttpGet("/DichVu/GetDropdown")]
        public async Task<IActionResult> GetDichVuDropdown()
        {
            var req = new DataTableRequest { Start = 0, Length = 1000 };
            var data = await _dichVuService.GetDanhSachDichVuAsync(req);
            return Ok(data.data.Select(x => new { value = x.DichVuId, text = $"{x.TenDichVu} ({x.DonVi})" }));
        }

        // ===== ĐĂNG KÝ DỊCH VỤ CHO PHÒNG =====

        [HttpGet("/DangKyDichVu/GetByPhong")]
        public async Task<IActionResult> GetDangKyDichVuByPhong([FromQuery] int phongTroId)
        {
            if (phongTroId <= 0) return BadRequest(new { Message = "PhongTroId không hợp lệ." });
            var data = await _dichVuService.GetDichVuVaDangKyCuaPhongAsync(CurrentActorId, phongTroId);
            if (data == null) return NotFound(new { Message = "Không tìm thấy phòng trọ." });
            return Ok(data);
        }

        [HttpPost("/DangKyDichVu/Luu")]
        public async Task<IActionResult> LuuDangKyDichVu([FromBody] DangKyDichVuReq request)
        {
            var result = await _dichVuService.LuuDangKyDichVuAsync(CurrentActorId, request);
            return result.Success ? Ok(new { Message = "Đăng ký dịch vụ thành công!" }) : this.ToErrorResult(result);
        }
    }
}


