using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using QuanLyChoThuePhongTroWeb.Web.Helpers;
using System;
using System.Threading.Tasks;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Controllers
{
    [ApiController]
    public class NguoiThueController : AdminBaseController
    {
        private readonly INguoiThueService _nguoiThueService;
        private readonly ILogger<NguoiThueController> _logger;

        public NguoiThueController(INguoiThueService nguoiThueService, ILogger<NguoiThueController> logger)
        {
            _nguoiThueService = nguoiThueService;
            _logger = logger;
        }

        [Route("QuanLyNhaTro/QuanLyNguoiThue")]
        [ApiExplorerSettings(IgnoreApi = true)]
        public async Task<IActionResult> QuanLyNguoiThue()
        {
            return View();
        }

        [HttpGet("/NguoiThue/GetAll")]
        public async Task<IActionResult> GetAll()
        {
            var data = await _nguoiThueService.GetAllAsync(CurrentActorId);
            return Ok(data);
        }

        [HttpGet("/NguoiThue/GetAvailable")]
        public async Task<IActionResult> GetAvailable()
        {
            var data = await _nguoiThueService.GetAvailableAsync(CurrentActorId);
            return Ok(data);
        }

        [HttpGet("/NguoiThue/GetCoHopDong")]
        public async Task<IActionResult> GetCoHopDong()
        {
            var data = await _nguoiThueService.DanhSachNguoiThueCoHopDongAsync(CurrentActorId);
            return Ok(data);
        }

        [HttpGet("/NguoiThue/GetById/{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var data = await _nguoiThueService.GetByIdForStaffAsync(CurrentActorId, id);
            if (data == null) return NotFound(new { Message = "Không tìm thấy người thuê" });
            return Ok(data);
        }

        [HttpPost("/NguoiThue/Create")]
        public async Task<IActionResult> Create([FromBody] NguoiThueReq request)
        {
            try
            {
                var result = await _nguoiThueService.CreateAsync(CurrentActorId, request);
                return result.Success ? Ok(new { Message = "Thêm người thuê thành công!" }) : this.ToErrorResult(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi thêm người thuê mới.");
                return StatusCode(500, new { Message = "Đã xảy ra lỗi hệ thống khi thêm người thuê." });
            }
        }

        [HttpPut("/NguoiThue/Update/{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] NguoiThueUpdateDto request)
        {
            try
            {
                if (id != request.NguoiThueId) return BadRequest(new { Message = "ID không khớp." });

                var result = await _nguoiThueService.UpdateAsync(CurrentActorId, id, request);
                return result.Success ? Ok(new { Message = "Cập nhật thành công!" }) : this.ToErrorResult(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi cập nhật người thuê {NguoiThueId}.", id);
                return StatusCode(500, new { Message = "Đã xảy ra lỗi hệ thống khi cập nhật người thuê." });
            }
        }

        [HttpDelete("/NguoiThue/Delete/{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var result = await _nguoiThueService.DeleteAsync(CurrentActorId, id);
                return result.Success ? Ok(new { Message = "Đã xóa người thuê." }) : this.ToErrorResult(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi xóa người thuê {NguoiThueId}.", id);
                return StatusCode(500, new { Message = "Đã xảy ra lỗi hệ thống khi xóa người thuê." });
            }
        }

        [HttpGet("/NguoiThue/SearchAutocomplete")]
        public async Task<IActionResult> SearchAutocomplete(string searchTerm = "")
        {
            var data = await _nguoiThueService.SearchAutocompleteAsync(CurrentActorId, searchTerm);
            return Ok(data);
        }

        [HttpPost("/NguoiThue/PhatSinhNgauNhien")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> PhatSinhNgauNhien()
        {
            try
            {
                var result = await _nguoiThueService.PhatSinhNgauNhienAsync(CurrentActorId);
                return result.Success ? Ok(new { success = true, message = result.Message }) : this.ToErrorResult(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi hệ thống khi sinh ngẫu nhiên người thuê.");
                return StatusCode(500, new { success = false, message = "Lỗi hệ thống khi phát sinh người thuê!" });
            }
        }
    }
}

