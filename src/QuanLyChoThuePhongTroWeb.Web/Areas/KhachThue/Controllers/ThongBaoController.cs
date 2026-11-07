using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuanLyChoThuePhongTroWeb.Filters;

namespace QuanLyChoThuePhongTroWeb.Areas.KhachThue.Controllers
{
    [Area("KhachThue")]
    [Authorize(Roles = "KhachThue")]
    [YeuCauDoiMatKhau]
    public class ThongBaoController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
