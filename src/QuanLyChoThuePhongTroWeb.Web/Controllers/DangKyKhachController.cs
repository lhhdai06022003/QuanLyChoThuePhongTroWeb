using Microsoft.AspNetCore.Mvc;

namespace QuanLyChoThuePhongTroWeb.Controllers;

[Route("dang-ky-khach")]
public sealed class DangKyKhachController : Controller
{
    [HttpGet("")]
    public IActionResult Index(string? returnUrl = null)
    {
        ViewBag.ReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null;
        return View();
    }
}
