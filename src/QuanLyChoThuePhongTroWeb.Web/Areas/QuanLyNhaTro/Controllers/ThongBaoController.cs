using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Controllers
{
    [Area("QuanLyNhaTro")]
    [Authorize(Roles = "Admin,NhanVien")]
    public class ThongBaoController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
