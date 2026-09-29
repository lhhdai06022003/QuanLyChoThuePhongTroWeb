using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuanLyChoThuePhongTroWeb.Application.Features.GiuChos.Services;

namespace QuanLyChoThuePhongTroWeb.Areas.KhachVangLai.Controllers;

[Area("KhachVangLai")]
[Authorize]
[Route("giu-cho/hoan-tien")]
public sealed class HoanTienController(IHoanTienGiuChoService refunds) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var id = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId) ? actorId : 0;
        return View(await refunds.GetMineAsync(id));
    }
}
