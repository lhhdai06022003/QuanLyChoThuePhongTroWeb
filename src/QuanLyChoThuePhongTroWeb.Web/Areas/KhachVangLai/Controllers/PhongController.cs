using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuanLyChoThuePhongTroWeb.Application.Features.PhongCongKhais.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.PhongCongKhais.Services;

namespace QuanLyChoThuePhongTroWeb.Areas.KhachVangLai.Controllers;

[Area("KhachVangLai")]
[AllowAnonymous]
[Route("phong")]
public sealed class PhongController(IPhongCongKhaiService rooms) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] PublicRoomQuery query)
    {
        ViewBag.Query = query;
        return View(await rooms.SearchAsync(query));
    }

    [HttpGet("{maCongKhai}")]
    public async Task<IActionResult> ChiTiet(string maCongKhai)
    {
        var room = await rooms.GetAsync(maCongKhai);
        return room is null ? NotFound() : View(room);
    }
}
