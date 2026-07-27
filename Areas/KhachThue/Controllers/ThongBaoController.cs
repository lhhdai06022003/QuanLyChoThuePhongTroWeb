using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace QuanLyChoThuePhongTroWeb.Areas.KhachThue.Controllers
{
    [Area("KhachThue")]
    [Authorize(Roles = "KhachThue")]
    public class ThongBaoController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
