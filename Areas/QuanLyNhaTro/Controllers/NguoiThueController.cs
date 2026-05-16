using Microsoft.AspNetCore.Mvc;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Services.NguoiThues;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Controllers
{
    [Area("QuanLyNhaTro")]
    [ApiController] // Thẻ này tự động trả về 400 BadRequest nếu DTO không hợp lệ
    public class NguoiThueController : Controller
    {
        private readonly INguoiThueService _nguoiThueService;

        public NguoiThueController(INguoiThueService nguoiThueService)
        {
            _nguoiThueService = nguoiThueService;
        }
        [Route("QuanLyNhaTro/QuanLyNguoiThue")]
        [ApiExplorerSettings(IgnoreApi = true)]
        public async Task<IActionResult> QuanLyNguoiThue()
        {
            // 1. Gọi Service để lấy danh sách chi nhánh từ Database
            //var danhSachChiNhanh = await _chiNhanhService.GetAllAsync();

            // 2. Trả danh sách này về cho file View (Index.cshtml)
            return View();
        }

        [HttpGet("/NguoiThue/GetAll")]
        public async Task<IActionResult> GetAll()
        {
            var data = await _nguoiThueService.GetAllAsync();
            return Ok(data);
        }

        [HttpGet("/NguoiThue/GetById/{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var data = await _nguoiThueService.GetByIdAsync(id);
            if (data == null) return NotFound(new { Message = "Không tìm thấy người thuê" });
            return Ok(data);
        }

        [HttpPost("/NguoiThue/Create")]
        public async Task<IActionResult> Create([FromBody] NguoiThueReq request)
        {
            // Controller cực gọn, mọi logic validation phức tạp đã nằm ở Service
            var result = await _nguoiThueService.CreateAsync(request);
            if (!result.IsSuccess)
            {
                return BadRequest(new { Message = result.ErrorMessage });
            }
            return Ok(new { Message = "Thêm người thuê thành công!" });
        }

        [HttpPut("/NguoiThue/Update/{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] NguoiThueUpdateDto request)
        {
            if (id != request.NguoiThueId) return BadRequest(new { Message = "ID không khớp." });

            var result = await _nguoiThueService.UpdateAsync(id, request);
            if (!result.IsSuccess)
            {
                return BadRequest(new { Message = result.ErrorMessage });
            }
            return Ok(new { Message = "Cập nhật thành công!" });
        }

        [HttpDelete("/NguoiThue/Delete/{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var result = await _nguoiThueService.DeleteAsync(id);
            if (!result.IsSuccess)
            {
                return BadRequest(new { Message = result.ErrorMessage });
            }
            return Ok(new { Message = "Đã xóa người thuê." });
        }
    }
}
