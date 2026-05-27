using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Services.NguoiDungs;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels;
using QuanLyChoThuePhongTroWeb.Models;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Controllers
{
    [Area("QuanLyNhaTro")]
    [Authorize] // Mặc định yêu cầu đăng nhập cho toàn bộ tính năng bên dưới
    public class NguoiDungController : Controller
    {
        private readonly INguoiDungService _nguoiDungService;

        public NguoiDungController(INguoiDungService nguoiDungService)
        {
            _nguoiDungService = nguoiDungService;
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

            // Tự động khởi tạo tài khoản admin mẫu nếu database trống
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

            // Thiết lập Cookie định danh thiết bị
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
                var innerMsg = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return Json(new { success = false, message = "Lỗi tạo tài khoản: " + innerMsg });
            }
        }

        // 4. API: Cập nhật tài khoản (ĐÃ SỬA LỖI ĐÈ TRACKER)
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
                // Lấy thực thể gốc đang được theo dõi từ DB lên để chỉnh sửa trực tiếp
                var existingUser = await _nguoiDungService.GetByIdAsync(id);
                if (existingUser == null)
                {
                    return Json(new { success = false, message = "Tài khoản không tồn tại trên hệ thống." });
                }

                // Cập nhật các thông tin thay đổi từ giao diện vào thực thể gốc
                existingUser.Role = dataGiaoDien.Role;
                existingUser.IsActive = dataGiaoDien.IsActive;

                // Nếu người dùng có gõ mật khẩu mới -> thay thế chuỗi mã băm cũ
                if (!string.IsNullOrEmpty(dataGiaoDien.MatKhauHash))
                {
                    existingUser.MatKhauHash = BamMatKhauSHA256(dataGiaoDien.MatKhauHash);
                }

                // Thực hiện cập nhật dòng dữ liệu (Tuyệt đối không dùng AddAsync tại đây)
                await _nguoiDungService.UpdateAsync(existingUser);

                return Json(new { success = true, message = "Cập nhật thông tin tài khoản thành công!" });
            }
            catch (Exception ex)
            {
                var innerMsg = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return Json(new { success = false, message = "Lỗi hệ thống lưu trữ thay đổi: " + innerMsg });
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
                var innerMsg = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return Json(new { success = false, message = "Lỗi không thể thực hiện xóa: " + innerMsg });
            }
        }

        [NonAction]
        private string BamMatKhauSHA256(string password)
        {
            using (System.Security.Cryptography.SHA256 sha256Hash = System.Security.Cryptography.SHA256.Create())
            {
                byte[] bytes = sha256Hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password));
                System.Text.StringBuilder builder = new System.Text.StringBuilder();
                for (int i = 0; i < bytes.Length; i++)
                {
                    builder.Append(bytes[i].ToString("x2"));
                }
                return builder.ToString();
            }
        }
    }
}