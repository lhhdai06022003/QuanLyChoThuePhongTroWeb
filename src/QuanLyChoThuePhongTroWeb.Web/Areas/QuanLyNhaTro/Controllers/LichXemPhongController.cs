using Microsoft.AspNetCore.Mvc;
using QuanLyChoThuePhongTroWeb.Application.Features.PhongTros.Services;
using QuanLyChoThuePhongTroWeb.Application.Features.PhongTros.DTOs;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Controllers;

public sealed class LichXemPhongController(IPhongTroService roomService,
    IConfiguration configuration) : AdminBaseController
{
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var publishedIds = configuration.GetSection("PublicRooms:RoomIds").Get<int[]>()?.ToHashSet();
        var rooms = await roomService.GetDanhSachPhongTroAsync(CurrentActorId);
        IReadOnlyList<PhongTroListItemDto> availableRooms = rooms
            .Where(room => room.TrangThai == 0 && room.DuocDangTin &&
                (publishedIds is null || publishedIds.Contains(room.PhongTroId)))
            .ToArray();
        return View(availableRooms);
    }
}
