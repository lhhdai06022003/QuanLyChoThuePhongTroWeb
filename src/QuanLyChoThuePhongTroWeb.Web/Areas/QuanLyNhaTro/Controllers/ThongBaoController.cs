using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuanLyChoThuePhongTroWeb.Filters;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Controllers
{
    [Area("QuanLyNhaTro")]
    [Authorize(Roles = "Admin,NhanVien")]
    [YeuCauDoiMatKhau]
    public class ThongBaoController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
