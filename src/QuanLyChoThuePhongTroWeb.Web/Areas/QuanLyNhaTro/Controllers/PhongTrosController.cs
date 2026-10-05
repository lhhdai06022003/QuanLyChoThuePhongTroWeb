using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Models;
using QuanLyChoThuePhongTroWeb.Web.Helpers;
using System;
using System.Threading.Tasks;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Controllers
{
    [Route("[controller]/[action]")]
    public class PhongTrosController : AdminBaseController
    {
        private readonly IPhongTroService _phongTroService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<PhongTrosController> _logger;

        public PhongTrosController(
            IPhongTroService phongTroService, 
            IConfiguration configuration,
            ILogger<PhongTrosController> logger)
        {
            _phongTroService = phongTroService;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<IActionResult> QuanLyPhongTro()
        {
            ViewBag.ChiNhanhs = await _phongTroService.GetDanhSachChiNhanhDropdownAsync(CurrentActorId);
            var section = _configuration.GetSection("VietQRSettings");
            ViewBag.BankId = section["BankId"] ?? "";
            ViewBag.AccountNumber = section["AccountNumber"] ?? "";
            ViewBag.AccountName = section["AccountName"] ?? "";
            return View();
        }

        public async Task<IActionResult> SoDoPhong()
        {
            ViewBag.ChiNhanhs = await _phongTroService.GetDanhSachChiNhanhDropdownAsync(CurrentActorId);
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetSoDoPhong([FromQuery] int chiNhanhId = 0)
        {
            var data = await _phongTroService.GetSoDoPhongAsync(CurrentActorId, chiNhanhId);
            return Json(data);
        }

        [HttpGet]
        public async Task<IActionResult> DanhSachPhongTro()
        {
            var data = await _phongTroService.GetDanhSachPhongTroAsync(CurrentActorId);
            return Json(new { data });
        }

        [HttpGet]
        public async Task<IActionResult> GetPhongTro(int id)
        {
            var result = await _phongTroService.GetPhongTroByIdAsync(CurrentActorId, id);
            return ToJson(result);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ThemPhongTro(QuanLyChoThuePhongTroWeb.Application.Features.PhongTros.DTOs.PhongTroReq model)
        {
            try
            {
                return ToJson(await _phongTroService.ThemPhongTroAsync(CurrentActorId, model));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi hệ thống khi thêm mới phòng trọ.");
                return Json(new { success = false, message = "Lỗi hệ thống khi thêm mới phòng!" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> CapNhatPhongTro(QuanLyChoThuePhongTroWeb.Application.Features.PhongTros.DTOs.PhongTroReq model)
        {
            try
            {
                return ToJson(await _phongTroService.CapNhatPhongTroAsync(CurrentActorId, model));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi hệ thống khi cập nhật phòng trọ.");
                return Json(new { success = false, message = "Lỗi hệ thống khi cập nhật phòng!" });
            }
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> XoaPhongTro(int id)
        {
            try
            {
                return ToJson(await _phongTroService.XoaPhongTroAsync(CurrentActorId, id));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi hệ thống khi xóa phòng trọ.");
                return Json(new { success = false, message = "Lỗi hệ thống khi xóa phòng!" });
            }
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> PhatSinhNgauNhien()
        {
            try
            {
                return ToJson(await _phongTroService.PhatSinhNgauNhienAsync(CurrentActorId));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi hệ thống khi phát sinh phòng ngẫu nhiên.");
                return Json(new { success = false, message = "Lỗi hệ thống khi phát sinh phòng!" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetQuickContract(int phongTroId)
        {
            if (phongTroId <= 0) return BadRequest(new { message = "ID phòng không hợp lệ." });

            try
            {
                var data = await _phongTroService.GetQuickContractAsync(CurrentActorId, phongTroId);
                if (data == null)
                {
                    return NotFound(new { message = "Không tìm thấy hợp đồng đang hoạt động cho phòng này." });
                }
                return Ok(data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy thông tin hợp đồng nhanh của phòng {PhongTroId}.", phongTroId);
                return StatusCode(500, new { message = "Đã xảy ra lỗi hệ thống khi lấy thông tin hợp đồng." });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetUnpaidInvoice(int phongTroId)
        {
            if (phongTroId <= 0) return BadRequest(new { message = "ID phòng không hợp lệ." });

            try
            {
                var data = await _phongTroService.GetUnpaidInvoiceAsync(CurrentActorId, phongTroId);
                if (data == null)
                {
                    return NotFound(new { message = "Không tìm thấy hóa đơn chưa thanh toán nào cho phòng này." });
                }
                return Ok(data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy hóa đơn chưa thanh toán của phòng {PhongTroId}.", phongTroId);
                return StatusCode(500, new { message = "Đã xảy ra lỗi hệ thống khi lấy thông tin hóa đơn." });
            }
        }

        // Lỗi nghiệp vụ giữ dạng 200 + success:false như giao diện đang dùng;
        // không có quyền (403) và không tìm thấy (404) trả đúng mã HTTP.
        private IActionResult ToJson(ServiceResult result)
        {
            if (!result.Success && result.ErrorKind != ServiceErrorKind.Validation)
            {
                return this.ToErrorResult(result);
            }

            return Json(new { success = result.Success, message = result.Message, data = result.Data });
        }
    }
}

