using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using QuanLyChoThuePhongTroWeb.Application.Features.LichXemPhongs.Services;
using QuanLyChoThuePhongTroWeb.Application.Features.LichXemPhongs.DTOs;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Controllers;

public sealed class LichXemPhongController(ILichXemPhongService viewings) : AdminBaseController
{
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var actorId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
        return View(await viewings.GetStaffQueueAsync(actorId));
    }

    [HttpGet]
    public async Task<IActionResult> KhungGioConTrong(int id)
    {
        var actorId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ? userId : 0;
        var slots = await viewings.GetRescheduleSlotsAsync(id, actorId);
        return Json(slots.Select(slot => new
        {
            id = slot.Id,
            label = $"{slot.BatDauUtc.AddHours(7):dd/MM/yyyy HH:mm}–{slot.KetThucUtc.AddHours(7):HH:mm} · còn {slot.SoChoConLai} chỗ"
        }));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> XacNhan(int id)
    {
        var actorId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ? userId : 0;
        if (!await viewings.DecideAsync(id, actorId, true)) return NotFound();
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TuChoi(int id)
    {
        var actorId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ? userId : 0;
        if (!await viewings.DecideAsync(id, actorId, false)) return NotFound();
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> XuLyLich(int id, ViewingStaffAction action, string reason, int? newSlotId)
    {
        var actorId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ? userId : 0;
        if (!ModelState.IsValid || (action == ViewingStaffAction.Reschedule && newSlotId is null) ||
            !await viewings.ManageConfirmedAsync(id, actorId, new(action, reason, null, newSlotId)))
            TempData["ViewingError"] = "Không thể cập nhật lịch. Khung giờ có thể đã hết chỗ; hãy chọn lại khung giờ còn trống.";
        else TempData["ViewingSuccess"] = "Đã cập nhật lịch xem.";
        return RedirectToAction(nameof(Index));
    }
}
