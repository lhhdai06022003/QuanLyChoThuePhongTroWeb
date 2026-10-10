using Microsoft.AspNetCore.Mvc;
using QuanLyChoThuePhongTroWeb.Application.Features.PhongTros.Services;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Controllers;

public sealed class TinPhongController(IPhongTroService roomService, IConfiguration configuration) : AdminBaseController
{
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var publishedIds = configuration.GetSection("PublicRooms:RoomIds").Get<int[]>()?.ToHashSet();
        var rooms = await roomService.GetDanhSachPhongTroAsync(CurrentActorId);
        var items = rooms.Select(room =>
        {
            var visible = room.TrangThai == 0 && room.DuocDangTin && room.ChiNhanhConHoatDong &&
                (publishedIds is null || publishedIds.Contains(room.PhongTroId));
            var cover = room.AnhDaiDienUrl;
            return new ListingPreviewItem(room.PhongTroId, room.SoPhong,
                room.TenChiNhanh ?? "", room.GiaThue, room.DienTich,
                room.TrangThai, visible, room.SoAnhDangHoatDong, cover);
        }).ToArray();

        return View(new ListingPreviewPage(items));
    }
}
