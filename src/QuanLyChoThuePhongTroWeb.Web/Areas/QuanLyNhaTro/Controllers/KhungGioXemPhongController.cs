using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using QuanLyChoThuePhongTroWeb.Application.Features.LichXemPhongs.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.LichXemPhongs.Services;
using QuanLyChoThuePhongTroWeb.Application.Features.PhongCongKhais.Services;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Controllers;

public sealed class KhungGioXemPhongController(IKhungGioXemPhongService slots, ITinPhongService rooms) : AdminBaseController
{
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        ViewBag.Phongs = await rooms.GetManageableAsync(ActorId);
        return View(await slots.GetManagedAsync(ActorId));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Tao(int phongTroId, DateTime batDau, DateTime ketThuc, int sucChua)
    {
        var saved = ModelState.IsValid && batDau.Year >= 2000 && ketThuc.Year >= 2000 &&
            await slots.CreateAsync(new CreateViewingSlotRequest(phongTroId,
                InVietnam(batDau), InVietnam(ketThuc), sucChua), ActorId);
        TempData[saved ? "SlotSuccess" : "SlotError"] = saved ? "Đã tạo khung giờ xem phòng."
            : "Không thể tạo khung giờ. Kiểm tra thời gian, sức chứa, phòng công khai và khung giờ trùng.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Dong(int id)
    {
        var saved = await slots.CloseAsync(id, ActorId);
        TempData[saved ? "SlotSuccess" : "SlotError"] = saved
            ? "Đã ngừng nhận lịch mới. Các lịch đã xác nhận được giữ nguyên." : "Không thể đóng khung giờ.";
        return RedirectToAction(nameof(Index));
    }

    private int ActorId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
    private static DateTimeOffset InVietnam(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Unspecified), TimeSpan.FromHours(7));
}
