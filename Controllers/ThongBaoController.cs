using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Services.ThongBaos;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace QuanLyChoThuePhongTroWeb.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class ThongBaoController : ControllerBase
    {
        private readonly IThongBaoService _thongBaoService;
        private readonly ILogger<ThongBaoController> _logger;

        public ThongBaoController(IThongBaoService thongBaoService, ILogger<ThongBaoController> logger)
        {
            _thongBaoService = thongBaoService;
            _logger = logger;
        }

        private int GetNguoiDungId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            return claim != null ? int.Parse(claim.Value) : 0;
        }

        [HttpGet("LayMoiNhat")]
        public async Task<IActionResult> LayMoiNhat(CancellationToken cancellationToken)
        {
            try
            {
                int nguoiDungId = GetNguoiDungId();
                if (nguoiDungId == 0) return Unauthorized();

                var thongBaos = await _thongBaoService.LayDanhSachTheoNguoiDungAsync(nguoiDungId, cancellationToken);
                var result = thongBaos.Select(t => new
                {
                    id = t.Id,
                    tieuDe = t.TieuDe,
                    noiDung = t.NoiDung,
                    linhVuc = t.LinhVuc,
                    linkDieuHuong = t.LinkDieuHuong,
                    isRead = t.IsRead,
                    createdAt = t.CreatedAt.AddHours(7).ToString("dd/MM/yyyy HH:mm")
                });
                return Ok(result);
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Lỗi lấy danh sách thông báo mới nhất");
                return StatusCode(500, "Lỗi hệ thống");
            }
        }

        [HttpGet("LaySoLuongChuaDoc")]
        public async Task<IActionResult> LaySoLuongChuaDoc(CancellationToken cancellationToken)
        {
            try
            {
                int nguoiDungId = GetNguoiDungId();
                if (nguoiDungId == 0) return Unauthorized();

                var count = await _thongBaoService.LaySoLuongChuaDocAsync(nguoiDungId, cancellationToken);
                return Ok(new { count });
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Lỗi lấy số lượng thông báo chưa đọc");
                return StatusCode(500, "Lỗi hệ thống");
            }
        }

        [HttpPost("DanhDauDaDoc/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DanhDauDaDoc(int id, CancellationToken cancellationToken)
        {
            try
            {
                int nguoiDungId = GetNguoiDungId();
                if (nguoiDungId == 0) return Unauthorized();

                var result = await _thongBaoService.DanhDauDaDocAsync(id, cancellationToken);
                return Ok(new { success = result.Success, message = result.Message });
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Lỗi đánh dấu đã đọc thông báo {Id}", id);
                return StatusCode(500, "Lỗi hệ thống");
            }
        }

        [HttpPost("DataTable")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DataTable([FromForm] QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels.Requests.DataTableRequest request, [FromForm] bool? chuaDoc, CancellationToken cancellationToken)
        {
            try
            {
                int nguoiDungId = GetNguoiDungId();
                if (nguoiDungId == 0) return Unauthorized();

                var response = await _thongBaoService.LayDanhSachPhanTrangAsync(request, nguoiDungId, chuaDoc, cancellationToken);
                
                // Format date for response
                var formattedData = response.data.Select(t => new
                {
                    t.Id,
                    t.TieuDe,
                    t.NoiDung,
                    t.LinhVuc,
                    t.LinkDieuHuong,
                    t.IsRead,
                    CreatedAt = t.CreatedAt.AddHours(7).ToString("dd/MM/yyyy HH:mm")
                });

                return Ok(new
                {
                    draw = response.draw,
                    recordsTotal = response.recordsTotal,
                    recordsFiltered = response.recordsFiltered,
                    data = formattedData
                });
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Lỗi lấy dữ liệu DataTable thông báo");
                return StatusCode(500, "Lỗi hệ thống");
            }
        }

        [HttpPost("DanhDauTatCaDaDoc")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DanhDauTatCaDaDoc(CancellationToken cancellationToken)
        {
            try
            {
                int nguoiDungId = GetNguoiDungId();
                if (nguoiDungId == 0) return Unauthorized();

                var result = await _thongBaoService.DanhDauTatCaDaDocAsync(nguoiDungId, cancellationToken);
                return Ok(new { success = result.Success, message = result.Message });
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Lỗi đánh dấu tất cả đã đọc");
                return StatusCode(500, "Lỗi hệ thống");
            }
        }

        [HttpDelete("Xoa/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Xoa(int id, CancellationToken cancellationToken)
        {
            try
            {
                int nguoiDungId = GetNguoiDungId();
                if (nguoiDungId == 0) return Unauthorized();

                var result = await _thongBaoService.XoaThongBaoAsync(id, nguoiDungId, cancellationToken);
                return Ok(new { success = result.Success, message = result.Message });
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Lỗi xóa thông báo {Id}", id);
                return StatusCode(500, "Lỗi hệ thống");
            }
        }

        [HttpDelete("XoaTatCa")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> XoaTatCa(CancellationToken cancellationToken)
        {
            try
            {
                int nguoiDungId = GetNguoiDungId();
                if (nguoiDungId == 0) return Unauthorized();

                var result = await _thongBaoService.XoaTatCaThongBaoAsync(nguoiDungId, cancellationToken);
                return Ok(new { success = result.Success, message = result.Message });
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Lỗi xóa tất cả thông báo");
                return StatusCode(500, "Lỗi hệ thống");
            }
        }
    }
}
