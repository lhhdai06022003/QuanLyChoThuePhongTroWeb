using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuanLyChoThuePhongTroWeb.Filters;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Controllers
{
    [Area("QuanLyNhaTro")]
    [Authorize(Roles = "Admin,NhanVien")]
    [AutoValidateAntiforgeryToken]
    [YeuCauDoiMatKhau]
    public class AdminBaseController : Controller
    {
        // Id người dùng đang đăng nhập; 0 khi thiếu claim, service coi như không có quyền (fail closed).
        protected int CurrentActorId =>
            int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) && id > 0 ? id : 0;
    }
}
