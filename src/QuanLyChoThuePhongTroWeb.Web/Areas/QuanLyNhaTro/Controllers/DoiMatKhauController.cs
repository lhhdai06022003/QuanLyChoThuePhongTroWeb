using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels;
using QuanLyChoThuePhongTroWeb.Filters;
using QuanLyChoThuePhongTroWeb.Web.Helpers;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Controllers
{
    // Admin và nhân viên tự đổi mật khẩu (spec bảo mật xác thực §2.5). Cả hai action nằm trong ngoại lệ của bộ lọc ép đổi.
    public class DoiMatKhauController : AdminBaseController
    {
        private const string DashboardUrl = "/QuanLyNhaTro/Dashboard";

        private readonly INguoiDungService _nguoiDungService;

        public DoiMatKhauController(INguoiDungService nguoiDungService)
        {
            _nguoiDungService = nguoiDungService;
        }

        [BoQuaYeuCauDoiMatKhau]
        [HttpGet("/QuanLyNhaTro/DoiMatKhau")]
        public IActionResult Index()
        {
            return View(new DoiMatKhauPageModel(ForcedPasswordChange.IsRequired(User)));
        }

        [BoQuaYeuCauDoiMatKhau]
        [HttpPost("/QuanLyNhaTro/DoiMatKhau")]
        public async Task<IActionResult> DoiMatKhau([FromBody] DoiMatKhauQuanLyReq? req)
        {
            if (req == null || !ModelState.IsValid)
            {
                var errors = string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                return BadRequest(new { success = false, message = string.IsNullOrEmpty(errors) ? "Dữ liệu gửi lên không hợp lệ." : errors });
            }

            if (req.MatKhauMoi != req.XacNhanMatKhau)
            {
                return BadRequest(new { success = false, message = "Mật khẩu xác nhận không khớp." });
            }

            if (CurrentActorId == 0)
            {
                return BadRequest(new { success = false, message = "Không xác định được tài khoản đang đăng nhập." });
            }

            var result = await _nguoiDungService.ChangePasswordAsync(CurrentActorId, req.MatKhauHienTai, req.MatKhauMoi);
            if (!result.Success)
            {
                return this.ToErrorResult(result);
            }

            if (ForcedPasswordChange.IsRequired(User))
            {
                await ForcedPasswordChange.ReissueWithoutClaimAsync(HttpContext);
            }

            return Json(new { success = true, message = result.Message, redirectUrl = DashboardUrl });
        }
    }
}
