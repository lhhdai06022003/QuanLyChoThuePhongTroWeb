using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuanLyChoThuePhongTroWeb.Application.Features.GiuChos.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.GiuChos.Services;
using QuanLyChoThuePhongTroWeb.Application.Features.PhongCongKhais.Services;
using QuanLyChoThuePhongTroWeb.Application.Features.KhachVangLais.Services;

namespace QuanLyChoThuePhongTroWeb.Areas.KhachVangLai.Controllers;

[Area("KhachVangLai")]
[Authorize]
[Route("giu-cho")]
public sealed class GiuChoController(IPhongCongKhaiService rooms, IGiuChoService reservations,
    IKhachVangLaiService guests, ITheoDoiGiuChoService progress) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index() => View(await reservations.GetMineAsync(ActorId));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> ChiTiet(int id)
    {
        var detail = await progress.GetMineAsync(id, ActorId);
        return detail is null ? NotFound() : View(detail);
    }

    [HttpGet("tao/{maCongKhai}")]
    [Authorize(Roles = "KhachVangLai,KhachThue")]
    public async Task<IActionResult> Tao(string maCongKhai, int? yeuCauXemPhongId = null)
    {
        var profile = await guests.GetProfileAsync(ActorId);
        if (profile is null) return Forbid();
        if (string.IsNullOrWhiteSpace(profile.Email) || string.IsNullOrWhiteSpace(profile.CCCD))
        {
            if (User.IsInRole("KhachThue"))
            {
                ViewBag.ProfileError = "Hồ sơ người thuê thiếu email hoặc CCCD. Vui lòng liên hệ quản lý để bổ sung trước khi giữ chỗ.";
            }
            else return RedirectToAction("HoSo", "TaiKhoan", new { area = "KhachVangLai",
                returnUrl = $"/giu-cho/tao/{Uri.EscapeDataString(maCongKhai)}" +
                    (yeuCauXemPhongId is int viewingId ? $"?yeuCauXemPhongId={viewingId}" : "") });
        }
        var room = await rooms.GetAsync(maCongKhai);
        if (room is null) return NotFound();
        ViewBag.YeuCauXemPhongId = yeuCauXemPhongId;
        return View(room);
    }

    [HttpPost("tao/{maCongKhai}")]
    [Authorize(Roles = "KhachVangLai,KhachThue")]
    [ActionName("Tao")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GuiYeuCau(string maCongKhai, int? yeuCauXemPhongId)
    {
        var result = await reservations.CreateAsync(
            new CreateReservationRequest(maCongKhai, yeuCauXemPhongId), ActorId);
        if (!result.Success)
        {
            TempData["ReservationError"] = result.Message;
            return RedirectToAction(nameof(Tao), new { maCongKhai, yeuCauXemPhongId });
        }
        return RedirectToAction(nameof(Index));
    }

    private int ActorId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
}
