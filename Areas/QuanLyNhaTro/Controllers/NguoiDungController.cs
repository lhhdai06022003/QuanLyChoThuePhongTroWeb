using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Services.NguoiDungs;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels;
using System.Security.Claims;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Controllers
{
    [Area("QuanLyNhaTro")]
    [Authorize]
    public class NguoiDungController : Controller
    {
        private readonly INguoiDungService _nguoiDungService;

        public NguoiDungController(INguoiDungService nguoiDungService)
        {
            _nguoiDungService = nguoiDungService;
        }

        [Route("QuanLyNhaTro/QuanLyTaiKhoanDangNhap")]
        public async Task<IActionResult> QuanLyTaiKhoanDangNhap()
        {
            return View();
        }

        [AllowAnonymous]
        [Route("QuanLyNhaTro/DangNhap")]
        [HttpGet]
        public async Task<IActionResult> DangNhap()
        {
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index", "Home", new { area = "" });
            }

            // Tự động tạo tài khoản admin nếu DB trống
            await _nguoiDungService.SeedAdminAccountAsync();

            return View();
        }

        [AllowAnonymous]
        [Route("QuanLyNhaTro/DangNhap")]
        [HttpPost]
        public async Task<IActionResult> DangNhap([FromForm] DangNhapReq req)
        {
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

            // Tạo Cookie Auth
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.NguoiDungId.ToString()),
                new Claim(ClaimTypes.Name, user.TenDangNhap),
                new Claim(ClaimTypes.Role, user.Role.ToString())
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);
            var props = new AuthenticationProperties
            {
                IsPersistent = req.GhiNho,
                ExpiresUtc = req.GhiNho ? DateTimeOffset.UtcNow.AddDays(30) : (DateTimeOffset?)null
            };

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, props);

            return RedirectToAction("Index", "Home", new { area = "" });
        }

        [AllowAnonymous]
        [Route("QuanLyNhaTro/DangXuat")]
        [HttpGet]
        public async Task<IActionResult> DangXuat()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("DangNhap", "NguoiDung", new { area = "QuanLyNhaTro" });
        }
    }
}
