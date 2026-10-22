using System;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using QuanLyChoThuePhongTroWeb.Application.Common.Configurations;
using QuanLyChoThuePhongTroWeb.Application.Common.Enums;
using QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.Services;
using QuanLyChoThuePhongTroWeb.Models.ChiSoDienNuoc;
using QuanLyChoThuePhongTroWeb.Web.Helpers;

namespace QuanLyChoThuePhongTroWeb.Areas.KhachThue.Controllers
{
    [Area("KhachThue")]
    [Authorize(Roles = "KhachThue")]
    [AutoValidateAntiforgeryToken]
    public class ChiSoDienNuocController : Controller
    {
        private readonly IMeterImagePortalService _portalService;
        private readonly MeterImageOptions _options;
        private readonly ILogger<ChiSoDienNuocController> _logger;

        public ChiSoDienNuocController(
            IMeterImagePortalService portalService,
            MeterImageOptions options,
            ILogger<ChiSoDienNuocController> logger)
        {
            _portalService = portalService ?? throw new ArgumentNullException(nameof(portalService));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [HttpGet("/KhachThue/ChiSoDienNuoc")]
        public IActionResult Index()
        {
            var (curMonth, curYear) = MeterPeriodPolicy.CurrentVn(DateTime.UtcNow);
            ViewBag.DanhSachKy = MeterPeriodPolicy.RecentPeriods(12);
            ViewBag.KyMacDinh = $"{curMonth:D2}/{curYear}";
            return View();
        }

        [HttpGet("/KhachThue/ChiSoDienNuoc/Phong")]
        public async Task<IActionResult> GetPhong([FromQuery] int thang, [FromQuery] int nam, CancellationToken ct)
        {
            var actorIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(actorIdStr, out var actorId) || actorId <= 0)
            {
                return Unauthorized(new { success = false, message = "Người dùng chưa đăng nhập hoặc phiên làm việc đã hết hạn." });
            }

            try
            {
                var result = await _portalService.GetTenantRoomsAsync(actorId, thang, nam, ct);
                if (!result.Success)
                {
                    return BadRequest(new { success = false, message = result.Message });
                }

                return Ok(new { success = true, data = result.Data });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy danh sách phòng của khách thuê {UserId} kỳ {Thang}/{Nam}", actorId, thang, nam);
                return StatusCode(500, new { success = false, message = "Có lỗi xảy ra trên máy chủ khi lấy danh sách phòng." });
            }
        }

        [HttpGet("/KhachThue/ChiSoDienNuoc/Ky")]
        public async Task<IActionResult> GetKy([FromQuery] int phongTroId, [FromQuery] int thang, [FromQuery] int nam, CancellationToken ct)
        {
            var actorIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(actorIdStr, out var actorId) || actorId <= 0)
            {
                return Unauthorized(new { success = false, message = "Người dùng chưa đăng nhập hoặc phiên làm việc đã hết hạn." });
            }

            try
            {
                var result = await _portalService.GetTenantPeriodAsync(actorId, phongTroId, thang, nam, ct);
                if (!result.Success)
                {
                    return BadRequest(new { success = false, message = result.Message });
                }

                return Ok(new { success = true, data = MeterImageVmMapper.ToTenantVm(result.Data!) });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy thông tin kỳ chỉ số phòng {PhongTroId} của khách thuê {UserId}", phongTroId, actorId);
                return StatusCode(500, new { success = false, message = "Có lỗi xảy ra trên máy chủ khi lấy thông tin kỳ chỉ số." });
            }
        }

        [HttpPost("/KhachThue/ChiSoDienNuoc/TaiAnh")]
        [RequestSizeLimit(6 * 1024 * 1024)]
        [RequestFormLimits(MultipartBodyLengthLimit = 6 * 1024 * 1024)]
        public async Task<IActionResult> TaiAnh([FromForm] UploadMeterImageFormReq req, CancellationToken ct)
        {
            var actorIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(actorIdStr, out var actorId) || actorId <= 0)
            {
                return Unauthorized(new { success = false, message = "Người dùng chưa đăng nhập hoặc phiên làm việc đã hết hạn." });
            }

            var (file, error) = await MeterImageUploadValidator.ValidateAsync(req.File, _options, ct);
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

                var result = await _portalService.UploadAsync(command, actorId, ct);
                if (!result.Success)
                {
                    return BadRequest(new { success = false, message = result.Message });
                }

                return Ok(new { success = true, message = result.Message, data = MeterImageVmMapper.ToTenantVm(result.Data!) });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi khách thuê {UserId} tải ảnh đồng hồ phòng {PhongTroId}, kỳ {Thang}/{Nam}", actorId, req.PhongTroId, req.Thang, req.Nam);
                return StatusCode(500, new { success = false, message = "Có lỗi xảy ra trên máy chủ khi tải ảnh." });
            }
        }
    }
}
