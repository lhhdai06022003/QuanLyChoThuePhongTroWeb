using Microsoft.AspNetCore.Mvc;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Controllers;

public sealed class GiuChoController : AdminBaseController
{
    [HttpGet]
    public IActionResult Index() => View();
}
