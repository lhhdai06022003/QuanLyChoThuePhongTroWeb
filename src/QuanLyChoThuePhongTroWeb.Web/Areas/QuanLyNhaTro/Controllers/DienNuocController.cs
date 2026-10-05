using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Security;
using QuanLyChoThuePhongTroWeb.Application.Common.Configurations;
using QuanLyChoThuePhongTroWeb.Application.Common.Enums;
using QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.Services;
using QuanLyChoThuePhongTroWeb.Application.Features.PhongTros.Services;
using QuanLyChoThuePhongTroWeb.Models.ChiSoDienNuoc;
using QuanLyChoThuePhongTroWeb.Web.Helpers;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Controllers
{
    [ApiController]
    public class DienNuocController : AdminBaseController
    {
        private readonly IDienNuocService _dienNuocService;
        private readonly IPhongTroService _phongTroService;
        private readonly ILogger<DienNuocController> _logger;
        private readonly IEmployeeAccessService _employeeAccessService;
        private readonly IMeterImagePortalService _meterImagePortalService;
        private readonly MeterImageOptions _meterImageOptions;

        public DienNuocController(
            IDienNuocService dienNuocService,
            IPhongTroService phongTroService,
            ILogger<DienNuocController> logger,
            IEmployeeAccessService employeeAccessService,
            IMeterImagePortalService meterImagePortalService,
            MeterImageOptions meterImageOptions)
        {
            _dienNuocService = dienNuocService;
            _phongTroService = phongTroService;
            _logger = logger;
            _employeeAccessService = employeeAccessService;
            _meterImagePortalService = meterImagePortalService;
            _meterImageOptions = meterImageOptions;
        }

        [Route("QuanLyNhaTro/ChotDienNuoc")]
        [ApiExplorerSettings(IgnoreApi = true)]
        public async Task<IActionResult> Index()
        {
            ViewBag.ListChiNhanh = await _phongTroService.GetDanhSachChiNhanhDropdownAsync(CurrentActorId);

            var chuaPhanCong = false;
            if (User.IsInRole("NhanVien") && int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId))
            {
                var scope = await _employeeAccessService.GetScopeAsync(actorId);
                chuaPhanCong = scope != null && !scope.IsAdmin && scope.ActiveBranchIds.Count == 0;
            }
            ViewBag.ChuaPhanCongChiNhanh = chuaPhanCong;

            return View();
        }

        [HttpGet("/DienNuoc/GetDanhSachPhongs")]
        public async Task<IActionResult> GetDanhSachPhongs(int chiNhanhId, int thang, int nam)
        {
            if (chiNhanhId <= 0 || thang < 1 || thang > 12 || nam < 2000)
            {
                return BadRequest(new { Message = "Tham số lọc không hợp lệ." });
            }

            var actorIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(actorIdStr, out var actorId) || actorId <= 0)
            {
                return Unauthorized(new { Message = "Người dùng chưa đăng nhập hoặc phiên làm việc đã hết hạn." });
            }

            try
            {
                var data = await _dienNuocService.GetDanhSachDienNuocAsync(chiNhanhId, thang, nam, actorId);
                return Ok(data);
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy danh sách điện nước chi nhánh {ChiNhanhId}, tháng {Thang}/{Nam}", chiNhanhId, thang, nam);
                return StatusCode(500, new { Message = "Có lỗi xảy ra trên máy chủ khi lấy dữ liệu." });
            }
        }

        [HttpPost("/DienNuoc/SaveChotDienNuoc")]
        public async Task<IActionResult> SaveChotDienNuoc([FromBody] ChotDienNuocReq request)
        {
            var actorIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(actorIdStr, out var actorId) || actorId <= 0)
            {
                return Unauthorized(new { Message = "Người dùng chưa đăng nhập hoặc phiên làm việc đã hết hạn." });
            }

            try
            {
                var result = await _dienNuocService.SaveChotDienNuocAsync(request, actorId);
                if (!result.IsSuccess)
                {
                    return BadRequest(new { Message = result.ErrorMessage });
                }
                return Ok(new { Message = "Lưu chỉ số điện nước thành công!" });
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lưu chốt điện nước chi nhánh {ChiNhanhId}, tháng {Thang}/{Nam}", request?.ChiNhanhId, request?.Thang, request?.Nam);
                return StatusCode(500, new { Message = "Lỗi hệ thống khi lưu chốt điện nước." });
            }
        }

        [HttpGet("/DienNuoc/AnhChiSo")]
        public async Task<IActionResult> GetAnhChiSo(int phongTroId, int thang, int nam)
        {
            var actorIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(actorIdStr, out var actorId) || actorId <= 0)
            {
                return Unauthorized(new { success = false, message = "Người dùng chưa đăng nhập hoặc phiên làm việc đã hết hạn." });
            }

            try
            {
                var result = await _meterImagePortalService.GetStaffPeriodAsync(actorId, phongTroId, thang, nam, HttpContext.RequestAborted);
                if (!result.Success)
                {
                    return BadRequest(new { success = false, message = result.Message });
                }

                return Ok(new { success = true, data = MeterImageVmMapper.ToStaffVm(result.Data!) });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy thông tin ảnh chỉ số phòng {PhongTroId}, tháng {Thang}/{Nam}", phongTroId, thang, nam);
                return StatusCode(500, new { success = false, message = "Có lỗi xảy ra trên máy chủ khi lấy dữ liệu." });
            }
        }

        [HttpPost("/DienNuoc/AnhChiSo/TaiLen")]
        [RequestSizeLimit(6 * 1024 * 1024)]
        [RequestFormLimits(MultipartBodyLengthLimit = 6 * 1024 * 1024)]
        public async Task<IActionResult> TaiLenAnhChiSo([FromForm] UploadMeterImageFormReq req)
        {
            var actorIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(actorIdStr, out var actorId) || actorId <= 0)
            {
                return Unauthorized(new { success = false, message = "Người dùng chưa đăng nhập hoặc phiên làm việc đã hết hạn." });
            }

            var (file, error) = await MeterImageUploadValidator.ValidateAsync(req.File, _meterImageOptions, HttpContext.RequestAborted);
            if (error != null)
            {
                return BadRequest(new { success = false, message = error });
            }

            try
            {
                var command = new MeterImageUploadCommand
                {
                    PhongTroId = req.PhongTroId,
                    Thang = req.Thang,
                    Nam = req.Nam,
                    LoaiDongHo = (AppLoaiDongHo)req.LoaiDongHo,
                    File = file!
                };

                var result = await _meterImagePortalService.UploadAsync(command, actorId, HttpContext.RequestAborted);
                if (!result.Success)
                {
                    return BadRequest(new { success = false, message = result.Message });
                }

                return Ok(new { success = true, message = result.Message, data = MeterImageVmMapper.ToStaffVm(result.Data!) });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi tải ảnh chỉ số phòng {PhongTroId}, tháng {Thang}/{Nam}", req.PhongTroId, req.Thang, req.Nam);
                return StatusCode(500, new { success = false, message = "Có lỗi xảy ra trên máy chủ khi tải ảnh." });
            }
        }

        [HttpPost("/DienNuoc/AnhChiSo/{id:int}/ThuLai")]
        public async Task<IActionResult> ThuLaiOcr(int id)
        {
            var actorIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(actorIdStr, out var actorId) || actorId <= 0)
            {
                return Unauthorized(new { success = false, message = "Người dùng chưa đăng nhập hoặc phiên làm việc đã hết hạn." });
            }

            try
            {
                var result = await _meterImagePortalService.RetryOcrAsync(id, actorId, HttpContext.RequestAborted);
                if (!result.Success)
                {
                    return BadRequest(new { success = false, message = result.Message });
                }

                return Ok(new { success = true, message = result.Message, data = MeterImageVmMapper.ToStaffVm(result.Data!) });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi thử lại nhận diện ảnh chỉ số {Id}", id);
                return StatusCode(500, new { success = false, message = "Có lỗi xảy ra trên máy chủ khi thử lại nhận diện." });
            }
        }

        [HttpPost("/DienNuoc/AnhChiSo/{id:int}/XacNhan")]
        public async Task<IActionResult> XacNhanAnhChiSo(int id, [FromBody] ConfirmMeterImageBodyReq body)
        {
            var actorIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(actorIdStr, out var actorId) || actorId <= 0)
            {
                return Unauthorized(new { success = false, message = "Người dùng chưa đăng nhập hoặc phiên làm việc đã hết hạn." });
            }

            try
            {
                var request = new ConfirmMeterImageRequest
                {
                    AnhChiSoDongHoId = id,
                    GiaTriXacNhan = body.GiaTriXacNhan,
                    GhiChu = body.GhiChu
                };

                var result = await _meterImagePortalService.ConfirmAsync(request, actorId, HttpContext.RequestAborted);
                if (!result.Success)
                {
                    return BadRequest(new { success = false, message = result.Message });
                }

                return Ok(new { success = true, message = result.Message, data = MeterImageVmMapper.ToStaffVm(result.Data!) });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi xác nhận ảnh chỉ số {Id}", id);
                return StatusCode(500, new { success = false, message = "Có lỗi xảy ra trên máy chủ khi xác nhận chỉ số." });
            }
        }

        [HttpPost("/DienNuoc/AnhChiSo/{id:int}/SuaSo")]
        public async Task<IActionResult> SuaSoAnhChiSo(int id, [FromBody] CorrectMeterImageBodyReq body)
        {
            var actorIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(actorIdStr, out var actorId) || actorId <= 0)
            {
                return Unauthorized(new { success = false, message = "Người dùng chưa đăng nhập hoặc phiên làm việc đã hết hạn." });
            }

            try
            {
                var request = new CorrectConfirmedMeterImageRequest(id, body.GiaTriMoi, body.LyDo);

                var result = await _meterImagePortalService.CorrectAsync(request, actorId, HttpContext.RequestAborted);
                if (!result.Success)
                {
                    return BadRequest(new { success = false, message = result.Message });
                }

                return Ok(new { success = true, message = result.Message, data = MeterImageVmMapper.ToStaffVm(result.Data!) });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi sửa số ảnh chỉ số {Id}", id);
                return StatusCode(500, new { success = false, message = "Có lỗi xảy ra trên máy chủ khi sửa số chỉ số." });
            }
        }
    }
}

