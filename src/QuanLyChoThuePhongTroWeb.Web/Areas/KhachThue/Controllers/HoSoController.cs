using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Linq;
using System.Diagnostics;
using QuanLyChoThuePhongTroWeb.Filters;
using QuanLyChoThuePhongTroWeb.Web.Helpers;

namespace QuanLyChoThuePhongTroWeb.Areas.KhachThue.Controllers
{
    [Area("KhachThue")]
    [Authorize(Roles = "KhachThue")]
    [YeuCauDoiMatKhau]
    public class HoSoController : Controller
    {
        private readonly INguoiThueService _nguoiThueService;
        private readonly INguoiDungService _nguoiDungService;

        public HoSoController(INguoiThueService nguoiThueService, INguoiDungService nguoiDungService)
        {
            _nguoiThueService = nguoiThueService;
            _nguoiDungService = nguoiDungService;
        }

        [BoQuaYeuCauDoiMatKhau]
        public async Task<IActionResult> Index()
        {
            if (!User.IsInRole("KhachThue"))
            {
                return Content("LỖI HỆ THỐNG: Truy cập bị từ chối. Tài khoản của bạn không có quyền Khách Thuê.");
            }

            var nguoiThueIdClaim = User.FindFirst("NguoiThueId");
            if (nguoiThueIdClaim == null) return Content("LỖI HỆ THỐNG: Tài khoản của bạn không được liên kết với hồ sơ người thuê nào (NguoiThueId bị trống). Vui lòng liên hệ Admin.");

            int nguoiThueId = int.Parse(nguoiThueIdClaim.Value);

            var nguoiThue = await _nguoiThueService.GetByIdAsync(nguoiThueId);

            if (nguoiThue == null) return NotFound();

            return View(nguoiThue);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [BoQuaYeuCauDoiMatKhau]
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
            
            var result = await _nguoiDungService.ChangePasswordAsync(userId, model.MatKhauCu, model.MatKhauMoi);
            if (!result.Success)
            {
                return Json(new { success = false, message = result.Message });
            }

            // Đổi xong mật khẩu ban đầu: phát hành lại cookie không còn claim ép đổi rồi cho vào trang chủ khách thuê.
            if (ForcedPasswordChange.IsRequired(User))
            {
                await ForcedPasswordChange.ReissueWithoutClaimAsync(HttpContext);
                return Json(new { success = true, message = result.Message, redirectUrl = "/KhachThue/Dashboard" });
            }

            return Json(new { success = true, message = result.Message });
        }
    }
}
