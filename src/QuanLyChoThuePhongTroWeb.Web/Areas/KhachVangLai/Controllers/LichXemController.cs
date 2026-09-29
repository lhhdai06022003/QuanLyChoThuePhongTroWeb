using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;
using QuanLyChoThuePhongTroWeb.Application.Features.LichXemPhongs.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.LichXemPhongs.Services;
using QuanLyChoThuePhongTroWeb.Application.Features.PhongCongKhais.Services;
using QuanLyChoThuePhongTroWeb.Application.Features.KhachVangLais.Services;
using QuanLyChoThuePhongTroWeb.Areas.KhachVangLai.ViewModels;

namespace QuanLyChoThuePhongTroWeb.Areas.KhachVangLai.Controllers;

[Area("KhachVangLai")]
[Route("lich-xem")]
public sealed class LichXemController(IPhongCongKhaiService rooms, ILichXemPhongService viewings,
    IKhachVangLaiService guests) : Controller
{
    [HttpGet("")]
    [Authorize]
    public async Task<IActionResult> Index()
    {
        var actorId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
        return View(await viewings.GetMineAsync(actorId));
    }

    [HttpGet("{maCongKhai}")]
    [Authorize(Roles = "KhachVangLai,KhachThue")]
    public async Task<IActionResult> Tao(string maCongKhai)
    {
        var room = await rooms.GetAsync(maCongKhai);
        if (room is null) return NotFound();
        var model = new TaoLichXemViewModel
        {
            MaCongKhai = maCongKhai,
            Phong = room,
            KhungGios = await viewings.GetOpenSlotsAsync(maCongKhai)
        };
        if (User.IsInRole("KhachThue") && int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId))
        {
            var profile = await guests.GetProfileAsync(actorId);
            if (profile is not null)
            {
                model.HoTen = profile.HoTen;
                model.SoDienThoai = profile.SoDienThoai;
                model.Email = profile.Email;
            }
        }
        return View(model);
    }

    [HttpPost("{maCongKhai}")]
    [Authorize(Roles = "KhachVangLai,KhachThue")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("guest-viewing")]
    public async Task<IActionResult> Tao(string maCongKhai, TaoLichXemViewModel model)
    {
        model.MaCongKhai = maCongKhai;
        ModelState.Remove(nameof(model.MaCongKhai));
        var room = await rooms.GetAsync(maCongKhai);
        if (room is null) return NotFound();
        model.Phong = room;
        model.KhungGios = await viewings.GetOpenSlotsAsync(maCongKhai);

        if (model.KhungGioXemPhongId is null && model.ThoiGianMongMuon is null)
            ModelState.AddModelError(nameof(model.ThoiGianMongMuon), "Chọn khung giờ hoặc nhập thời gian mong muốn.");

        if (ModelState.IsValid)
        {
            // The public form uses Vietnam local time (UTC+7); the service and database use UTC.
            var desired = model.ThoiGianMongMuon ?? DateTime.UtcNow.AddDays(1);
            var desiredOffset = new DateTimeOffset(DateTime.SpecifyKind(desired, DateTimeKind.Unspecified),
                TimeSpan.FromHours(7));
            int? actorId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
            var result = await viewings.CreateAsync(new CreateViewingRequest(maCongKhai,
                model.KhungGioXemPhongId, desiredOffset, model.HoTen, model.SoDienThoai,
                model.Email, model.GhiChu), actorId);
            if (result.Success) return RedirectToAction(nameof(DaGui));
            ModelState.AddModelError(string.Empty, result.Message);
        }

        return View(model);
    }

    [HttpGet("da-gui")]
    [Authorize(Roles = "KhachVangLai,KhachThue")]
    public IActionResult DaGui() => View();
}
