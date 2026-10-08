using Microsoft.AspNetCore.Mvc;
using QuanLyChoThuePhongTroWeb.Application.Features.PhongTros.Services;
using QuanLyChoThuePhongTroWeb.Application.Features.PhongTros.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.Viewings;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Controllers;

public sealed class LichXemPhongController(IPhongTroService roomService,
    IViewingSlotAdminService slots, IViewingAdminRequestService requests,
    IConfiguration configuration) : AdminBaseController
{
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var publishedIds = configuration.GetSection("PublicRooms:RoomIds").Get<int[]>()?.ToHashSet();
        var rooms = await roomService.GetDanhSachPhongTroAsync(CurrentActorId);
        IReadOnlyList<PhongTroListItemDto> availableRooms = rooms
            .Where(room => room.TrangThai == 0 && room.DuocDangTin && room.ChiNhanhConHoatDong &&
                (publishedIds is null || publishedIds.Contains(room.PhongTroId)))
            .ToArray();
        var slotList = await slots.ListAsync(
            availableRooms.Select(room => room.PhongTroId).ToArray());
        return View(new ViewingAdminPageModel(availableRooms, slotList,
            await requests.ListAsync(CurrentActorId)));
    }

    [HttpPost]
    public async Task<IActionResult> TaoKhungGio(CreateViewingSlotModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            TempData["ViewingAdminMessage"] = "Thông tin khung giờ không hợp lệ.";
            return RedirectToAction(nameof(Index));
        }

        var start = new DateTimeOffset(DateTime.SpecifyKind(model.StartLocal,
            DateTimeKind.Unspecified), TimeSpan.FromHours(7)).UtcDateTime;
        var end = new DateTimeOffset(DateTime.SpecifyKind(model.EndLocal,
            DateTimeKind.Unspecified), TimeSpan.FromHours(7)).UtcDateTime;
        var result = await slots.CreateAsync(CurrentActorId, model.RoomId,
            start, end, model.Capacity, cancellationToken);
        TempData["ViewingAdminMessage"] = result.Message;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> XuLyYeuCau(int requestId,
        ViewingAdminAction action, DateTime? confirmedTimeLocal,
        string? reason, CancellationToken cancellationToken)
    {
        DateTime? confirmedUtc = confirmedTimeLocal.HasValue
            ? new DateTimeOffset(DateTime.SpecifyKind(confirmedTimeLocal.Value,
                DateTimeKind.Unspecified), TimeSpan.FromHours(7)).UtcDateTime
            : null;
        var result = await requests.UpdateAsync(CurrentActorId, requestId,
            action, confirmedUtc, reason, cancellationToken);
        TempData["ViewingAdminMessage"] = result.Message;
        return RedirectToAction(nameof(Index));
    }
}
