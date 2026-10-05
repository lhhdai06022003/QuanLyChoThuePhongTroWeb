using Microsoft.AspNetCore.Mvc;
using QuanLyChoThuePhongTroWeb.Web.Helpers;

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
            var data = await _thanhVienService.GetThanhVienByHopDongIdAsync(CurrentActorId, hopDongId);
            return Ok(data);
        }

        [HttpPost("/ThanhVienHopDong/AddThanhVien")]
        public async Task<IActionResult> AddThanhVien([FromBody] ThanhVienHopDongReq request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { Message = "Dữ liệu không hợp lệ." });
            }

            var result = await _thanhVienService.AddThanhVienVaoHopDongAsync(CurrentActorId, request);
            return result.Success ? Ok(new { Message = "Thêm thành viên thành công!" }) : this.ToErrorResult(result);
        }

        [HttpPost("/ThanhVienHopDong/BaoRoiPhong/{chiTietId}")]
        public async Task<IActionResult> BaoRoiPhong(int chiTietId)
        {
            var result = await _thanhVienService.BaoRoiPhongAsync(CurrentActorId, chiTietId);
            return result.Success ? Ok(new { Message = "Đã báo rời phòng thành công!" }) : this.ToErrorResult(result);
        }

        [HttpDelete("/ThanhVienHopDong/XoaThanhVien/{chiTietId}")]
        public async Task<IActionResult> XoaThanhVien(int chiTietId)
        {
            var result = await _thanhVienService.XoaThanhVienNhamAsync(CurrentActorId, chiTietId);
            return result.Success ? Ok(new { Message = "Đã xoá thành viên thành công!" }) : this.ToErrorResult(result);
        }
    }
}

