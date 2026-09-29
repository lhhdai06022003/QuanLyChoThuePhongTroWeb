using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels;
using QuanLyChoThuePhongTroWeb.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Controllers
{
    public class NguoiDungController : AdminBaseController
    {
        private readonly INguoiDungService _nguoiDungService;
        private readonly ILogger<NguoiDungController> _logger;

        public NguoiDungController(INguoiDungService nguoiDungService, ILogger<NguoiDungController> logger)
        {
            _nguoiDungService = nguoiDungService;
            _logger = logger;
        }

        [AllowAnonymous]
        [Route("QuanLyNhaTro/DangNhap")]
        [HttpGet]
        public IActionResult DangNhap(string? returnUrl = null)
        {
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                if (User.IsInRole("KhachThue"))
                {
                    return RedirectToAction("Index", "Dashboard", new { area = "KhachThue" });
                }
                return RedirectToAction("Index", "Dashboard", new { area = "QuanLyNhaTro" });
            }

            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        [AllowAnonymous]
        [Route("QuanLyNhaTro/DangNhap")]
        [HttpPost]
        public async Task<IActionResult> DangNhap([FromForm] DangNhapReq req, string? returnUrl = null)
        {
            ViewBag.ReturnUrl = returnUrl;

            if (!ModelState.IsValid)
            {
                ViewBag.Error = "Vui lòng nhập đầy đủ thông tin.";
                return View(req);
            }

            var user = await _nguoiDungService.ValidateUserAsync(req.TenDangNhap, req.MatKhau);

            if (user == null)
            {
                ViewBag.Error = "Tên đăng nhập hoặc mật khẩu không chính xác.";
                return View(req);
            }

            // Thiết lập Cookie định danh thiết bị
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.NguoiDungId.ToString()),
                new Claim(ClaimTypes.Name, user.TenDangNhap),
                new Claim(ClaimTypes.Role, user.Role.ToString())
            };

            if (user.Role == QuanLyChoThuePhongTroWeb.Application.Common.Enums.AppRole.KhachThue && user.NguoiThueId.HasValue)
            {
                claims.Add(new Claim("NguoiThueId", user.NguoiThueId.Value.ToString()));
            }

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme, ClaimTypes.Name, ClaimTypes.Role);
            var principal = new ClaimsPrincipal(identity);
            var props = new AuthenticationProperties
            {
                IsPersistent = req.GhiNho,
                ExpiresUtc = req.GhiNho ? DateTimeOffset.UtcNow.AddDays(30) : (DateTimeOffset?)null
            };

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, props);

            if (user.Role == QuanLyChoThuePhongTroWeb.Application.Common.Enums.AppRole.KhachThue)
            {
                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) && !returnUrl.Contains("/QuanLyNhaTro", StringComparison.OrdinalIgnoreCase))
                {
                    return Redirect(returnUrl);
                }
                return RedirectToAction("Index", "Dashboard", new { area = "KhachThue" });
            }

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction("Index", "Dashboard", new { area = "QuanLyNhaTro" });
        }

        [AllowAnonymous]
        [Route("QuanLyNhaTro/DangXuat")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DangXuat()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("DangNhap", "NguoiDung", new { area = "QuanLyNhaTro" });
        }

        [Route("QuanLyNhaTro/QuanLyTaiKhoanDangNhap")]
        public IActionResult QuanLyTaiKhoanDangNhap()
        {
            // Trả về View tĩnh, dữ liệu sẽ được Datatable gọi Ajax nạp lên sau
            return View();
        }

        // 1. API: Lấy danh sách tài khoản
        [HttpGet]
        [Route("QuanLyNhaTro/NguoiDung/GetAllApi")]
        public async Task<IActionResult> GetAllApi()
        {
            var danhSach = await _nguoiDungService.GetAllAsync();
            return Json(danhSach);
        }

        // 2. API: Lấy chi tiết tài khoản theo ID
        [HttpGet]
        [Route("QuanLyNhaTro/NguoiDung/GetByIdApi/{id}")]
        public async Task<IActionResult> GetByIdApi(int id)
        {
            var nguoiDung = await _nguoiDungService.GetByIdAsync(id);
            if (nguoiDung == null)
            {
                return NotFound();
            }
            return Json(nguoiDung);
        }

        // 3. API: Thêm mới tài khoản
        [HttpPost]
        [Route("QuanLyNhaTro/NguoiDung/CreateApi")]
        public async Task<IActionResult> CreateApi([FromBody] QuanLyChoThuePhongTroWeb.Application.Features.NguoiDungs.DTOs.CreateNguoiDungReq nguoiDung)
        {
            if (nguoiDung.Role == QuanLyChoThuePhongTroWeb.Application.Common.Enums.AppRole.KhachThue && !nguoiDung.NguoiThueId.HasValue)
            {
                return Json(new { success = false, message = "Vui lòng chọn người thuê khi tạo tài khoản Khách Thuê." });
            }

            if (!ModelState.IsValid)
            {
                return Json(new { success = false, message = "Dữ liệu form gửi lên không hợp lệ." });
            }

            try
            {
                var result = await _nguoiDungService.AddAsync(nguoiDung);
                return Json(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi thêm mới tài khoản.");
                return Json(ServiceResult.Fail("Lỗi hệ thống khi tạo tài khoản!"));
            }
        }

        // 4. API: Cập nhật tài khoản
        [HttpPost]
        [Route("QuanLyNhaTro/NguoiDung/EditApi/{id}")]
        public async Task<IActionResult> EditApi(int id, [FromBody] QuanLyChoThuePhongTroWeb.Application.Features.NguoiDungs.DTOs.EditNguoiDungReq dataGiaoDien)
        {
            if (id != dataGiaoDien.NguoiDungId)
            {
                return Json(new { success = false, message = "Mã định danh tài khoản bất đồng bộ." });
            }

            if (dataGiaoDien.Role == QuanLyChoThuePhongTroWeb.Application.Common.Enums.AppRole.KhachThue && !dataGiaoDien.NguoiThueId.HasValue)
            {
                return Json(new { success = false, message = "Vui lòng chọn người thuê khi đặt quyền thành Khách Thuê." });
            }

            try
            {
                var result = await _nguoiDungService.UpdateUserAsync(id, dataGiaoDien.Role, dataGiaoDien.IsActive, dataGiaoDien.MatKhauMoi, dataGiaoDien.NguoiThueId);
                return Json(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi cập nhật tài khoản {NguoiDungId}.", id);
                return Json(ServiceResult.Fail("Lỗi hệ thống khi lưu trữ thay đổi tài khoản!"));
            }
        }

        // 5. API: Xóa mềm tài khoản
        [HttpPost]
        [Route("QuanLyNhaTro/NguoiDung/DeleteApi/{id}")]
        public async Task<IActionResult> DeleteApi(int id)
        {
            try
            {
                var result = await _nguoiDungService.DeleteAsync(id);
                return Json(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi xóa tài khoản {NguoiDungId}.", id);
                return Json(ServiceResult.Fail("Lỗi hệ thống khi xóa tài khoản!"));
            }
        }

        // 6. API: Đặt lại mật khẩu về số điện thoại
        [HttpPost]
        [Route("QuanLyNhaTro/NguoiDung/ResetPasswordToPhoneApi/{id}")]
        public async Task<IActionResult> ResetPasswordToPhoneApi(int id)
        {
            try
            {
                var result = await _nguoiDungService.ResetPasswordToPhoneAsync(id);
                return Json(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi đặt lại mật khẩu cho tài khoản {NguoiDungId}.", id);
                return Json(ServiceResult.Fail("Lỗi hệ thống khi đặt lại mật khẩu!"));
            }
        }
    }
}
