using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Services.NguoiDungs;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels;
using QuanLyChoThuePhongTroWeb.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Controllers
{
    [Area("QuanLyNhaTro")]
    [Authorize] // Mặc định yêu cầu đăng nhập cho toàn bộ tính năng bên dưới
    [AutoValidateAntiforgeryToken]
    public class NguoiDungController : Controller
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
                return RedirectToAction("Index", "Home", new { area = "" });
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

            if (user.Role == Role.KhachThue && user.NguoiThueId.HasValue)
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

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            if (user.Role == Role.KhachThue)
            {
                return RedirectToAction("Index", "Dashboard", new { area = "KhachThue" });
            }

            return RedirectToAction("Index", "Home", new { area = "" });
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
        public async Task<IActionResult> CreateApi([FromBody] NguoiDung nguoiDung)
        {
            if (!ModelState.IsValid)
            {
                return Json(new { success = false, message = "Dữ liệu form gửi lên không hợp lệ." });
            }

            try
            {
                // Tầng service AddAsync của bạn đã tự động băm SHA256 dựa vào trường MatKhauHash gửi lên
                await _nguoiDungService.AddAsync(nguoiDung);
                return Json(new { success = true, message = "Thêm mới tài khoản thành công!" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi thêm mới tài khoản.");
                return Json(new { success = false, message = "Lỗi hệ thống khi tạo tài khoản!" });
            }
        }

        // 4. API: Cập nhật tài khoản
        [HttpPost]
        [Route("QuanLyNhaTro/NguoiDung/EditApi/{id}")]
        public async Task<IActionResult> EditApi(int id, [FromBody] NguoiDung dataGiaoDien)
        {
            if (id != dataGiaoDien.NguoiDungId)
            {
                return Json(new { success = false, message = "Mã định danh tài khoản bất đồng bộ." });
            }

            try
            {
                var success = await _nguoiDungService.UpdateUserAsync(id, dataGiaoDien.Role, dataGiaoDien.IsActive, dataGiaoDien.MatKhauHash);
                if (!success)
                {
                    return Json(new { success = false, message = "Tài khoản không tồn tại trên hệ thống." });
                }

                return Json(new { success = true, message = "Cập nhật thông tin tài khoản thành công!" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi cập nhật tài khoản {NguoiDungId}.", id);
                return Json(new { success = false, message = "Lỗi hệ thống khi lưu trữ thay đổi tài khoản!" });
            }
        }

        // 5. API: Xóa mềm tài khoản
        [HttpPost]
        [Route("QuanLyNhaTro/NguoiDung/DeleteApi/{id}")]
        public async Task<IActionResult> DeleteApi(int id)
        {
            try
            {
                var user = await _nguoiDungService.GetByIdAsync(id);
                if (user == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy tài khoản để xóa." });
                }

                await _nguoiDungService.DeleteAsync(id);
                return Json(new { success = true, message = "Đã xóa tài khoản ra khỏi hệ thống quản lý." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi xóa tài khoản {NguoiDungId}.", id);
                return Json(new { success = false, message = "Lỗi hệ thống khi xóa tài khoản!" });
            }
        }
    }
}