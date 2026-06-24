using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Services.DieuKhoanMaus;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels.Requests;
using System.Threading.Tasks;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Controllers
{
    [Area("QuanLyNhaTro")]
    [Authorize(Roles = "Admin,NhanVien")]
    public class DieuKhoanMauController : Controller
    {
        private readonly IDieuKhoanMauService _dieuKhoanMauService;

        public DieuKhoanMauController(IDieuKhoanMauService dieuKhoanMauService)
        {
            _dieuKhoanMauService = dieuKhoanMauService;
        }

        public async Task<IActionResult> Index()
        {
            var data = await _dieuKhoanMauService.GetAllAsync();
            return View(data);
        }

        [HttpGet("/DieuKhoanMau/GetById/{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var data = await _dieuKhoanMauService.GetByIdAsync(id);
            if (data == null) return NotFound(new { message = "Không tìm thấy điều khoản" });
            return Ok(data);
        }

        [HttpPost("/DieuKhoanMau/Create")]
        public async Task<IActionResult> Create([FromBody] DieuKhoanMauReq request)
        {
            if (!ModelState.IsValid) return BadRequest(new { message = "Dữ liệu không hợp lệ" });

            var result = await _dieuKhoanMauService.CreateAsync(request);
            if (result.Success) return Ok(new { message = result.Message });
            
            return BadRequest(new { message = result.Message });
        }

        [HttpPut("/DieuKhoanMau/Update/{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] DieuKhoanMauReq request)
        {
            if (!ModelState.IsValid) return BadRequest(new { message = "Dữ liệu không hợp lệ" });
            
            request.DieuKhoanMauId = id;
            var result = await _dieuKhoanMauService.UpdateAsync(request);
            if (result.Success) return Ok(new { message = result.Message });
            
            return BadRequest(new { message = result.Message });
        }

        [HttpDelete("/DieuKhoanMau/Delete/{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var result = await _dieuKhoanMauService.DeleteAsync(id);
            if (result.Success) return Ok(new { message = result.Message });
            
            return BadRequest(new { message = result.Message });
        }
    }
}

