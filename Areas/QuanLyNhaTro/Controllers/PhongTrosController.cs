using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Services.PhongTros;
using QuanLyChoThuePhongTroWeb.Models;
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
            ViewBag.ChiNhanhs = await _phongTroService.GetDanhSachChiNhanhDropdownAsync();
            var section = _configuration.GetSection("VietQRSettings");
            ViewBag.BankId = section["BankId"] ?? "";
            ViewBag.AccountNumber = section["AccountNumber"] ?? "";
            ViewBag.AccountName = section["AccountName"] ?? "";
            return View();
        }

        public async Task<IActionResult> SoDoPhong()
        {
            ViewBag.ChiNhanhs = await _phongTroService.GetDanhSachChiNhanhDropdownAsync();
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetSoDoPhong([FromQuery] int chiNhanhId = 0)
        {
            var data = await _phongTroService.GetSoDoPhongAsync(chiNhanhId);
            return Json(data);
        }

        [HttpGet]
        public async Task<IActionResult> DanhSachPhongTro()
        {
            var data = await _phongTroService.GetDanhSachPhongTroAsync();
            return Json(new { data });
        }

        [HttpGet]
        public async Task<IActionResult> GetPhongTro(int id)
        {
            var result = await _phongTroService.GetPhongTroByIdAsync(id);
            return Json(new { success = result.Success, message = result.Message, data = result.Data });
        }

        [HttpPost]
        public async Task<IActionResult> ThemPhongTro(PhongTro model)
        {
            try
            {
                var result = await _phongTroService.ThemPhongTroAsync(model);
                return Json(new { success = result.Success, message = result.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi hệ thống khi thêm mới phòng trọ.");
                return Json(new { success = false, message = "Lỗi hệ thống khi thêm mới phòng!" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> CapNhatPhongTro(PhongTro model)
        {
            try
            {
                var result = await _phongTroService.CapNhatPhongTroAsync(model);
                return Json(new { success = result.Success, message = result.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi hệ thống khi cập nhật phòng trọ.");
                return Json(new { success = false, message = "Lỗi hệ thống khi cập nhật phòng!" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> XoaPhongTro(int id)
        {
            try
            {
                var result = await _phongTroService.XoaPhongTroAsync(id);
                return Json(new { success = result.Success, message = result.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi hệ thống khi xóa phòng trọ.");
                return Json(new { success = false, message = "Lỗi hệ thống khi xóa phòng!" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> PhatSinhNgauNhien()
        {
            try
            {
                var result = await _phongTroService.PhatSinhNgauNhienAsync();
                return Json(new { success = result.Success, message = result.Message });
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
                var data = await _phongTroService.GetQuickContractAsync(phongTroId);
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
                var data = await _phongTroService.GetUnpaidInvoiceAsync(phongTroId);
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
    }
}

