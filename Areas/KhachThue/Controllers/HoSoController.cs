using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyChoThuePhongTroWeb.Data;
using System.Security.Claims;
using System.Threading.Tasks;

using System.Linq;

namespace QuanLyChoThuePhongTroWeb.Areas.KhachThue.Controllers
{
    [Area("KhachThue")]
    [Authorize]
    public class HoSoController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HoSoController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            if (!User.IsInRole("KhachThue"))
            {
                return Content("LỖI HỆ THỐNG: Truy cập bị từ chối. Tài khoản của bạn không có quyền Khách Thuê.");
            }

            var nguoiThueIdClaim = User.FindFirst("NguoiThueId");
            if (nguoiThueIdClaim == null) return Content("LỖI HỆ THỐNG: Tài khoản của bạn không được liên kết với hồ sơ người thuê nào (NguoiThueId bị trống). Vui lòng liên hệ Admin.");

            int nguoiThueId = int.Parse(nguoiThueIdClaim.Value);

            var nguoiThue = await _context.NguoiThues
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.NguoiThueId == nguoiThueId && !x.IsDeleted);

            if (nguoiThue == null) return NotFound();

            return View(nguoiThue);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DoiMatKhau([FromBody] QuanLyChoThuePhongTroWeb.Areas.KhachThue.ViewModels.DoiMatKhauViewModel model)
        {
            if (!ModelState.IsValid)
            {
                var errors = string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                return Json(new { success = false, message = errors });
            }

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null) return Json(new { success = false, message = "Không xác định được danh tính người dùng." });

            int userId = int.Parse(userIdClaim.Value);
            var user = await _context.NguoiDungs.FirstOrDefaultAsync(u => u.NguoiDungId == userId && !u.IsDeleted);

            if (user == null) return Json(new { success = false, message = "Tài khoản không tồn tại." });

            string hashedOldPassword = BamMatKhauSHA256(model.MatKhauCu);
            if (user.MatKhauHash != hashedOldPassword)
            {
                return Json(new { success = false, message = "Mật khẩu hiện tại không chính xác." });
            }

            user.MatKhauHash = BamMatKhauSHA256(model.MatKhauMoi);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Đổi mật khẩu thành công!" });
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
