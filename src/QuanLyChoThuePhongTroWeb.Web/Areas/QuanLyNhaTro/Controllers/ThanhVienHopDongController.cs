using Microsoft.AspNetCore.Mvc;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Controllers
{
    [ApiController]
    public class ThanhVienHopDongController : AdminBaseController
    {
        private readonly IThanhVienHopDongService _thanhVienService;

        public ThanhVienHopDongController(IThanhVienHopDongService thanhVienService)
        {
            _thanhVienService = thanhVienService;
        }

        [HttpGet("/ThanhVienHopDong/GetDanhSach/{hopDongId}")]
        public async Task<IActionResult> GetDanhSach(int hopDongId)
        {
            var data = await _thanhVienService.GetThanhVienByHopDongIdAsync(hopDongId);
            return Ok(data);
        }

        [HttpPost("/ThanhVienHopDong/AddThanhVien")]
        public async Task<IActionResult> AddThanhVien([FromBody] ThanhVienHopDongReq request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { Message = "Dữ liệu không hợp lệ." });
            }

            var result = await _thanhVienService.AddThanhVienVaoHopDongAsync(request);
            if (!result.IsSuccess)
            {
                return BadRequest(new { Message = result.ErrorMessage });
            }
            return Ok(new { Message = "Thêm thành viên thành công!" });
        }

        [HttpPost("/ThanhVienHopDong/BaoRoiPhong/{chiTietId}")]
        public async Task<IActionResult> BaoRoiPhong(int chiTietId)
        {
            var result = await _thanhVienService.BaoRoiPhongAsync(chiTietId);
            if (!result.IsSuccess)
            {
                return BadRequest(new { Message = result.ErrorMessage });
            }
            return Ok(new { Message = "Đã báo rời phòng thành công!" });
        }

        [HttpDelete("/ThanhVienHopDong/XoaThanhVien/{chiTietId}")]
        public async Task<IActionResult> XoaThanhVien(int chiTietId)
        {
            var result = await _thanhVienService.XoaThanhVienNhamAsync(chiTietId);
            if (!result.IsSuccess)
            {
                return BadRequest(new { Message = result.ErrorMessage });
            }
            return Ok(new { Message = "Đã xoá thành viên thành công!" });
        }
    }
}

