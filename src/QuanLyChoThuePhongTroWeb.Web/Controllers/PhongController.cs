using Microsoft.AspNetCore.Mvc;
using QuanLyChoThuePhongTroWeb.Application.Features.PublicRooms;

namespace QuanLyChoThuePhongTroWeb.Controllers;

[Route("phong")]
public sealed class PhongController(IPublicRoomService rooms, IConfiguration configuration) : Controller
{
    private int[] AllowedRoomIds =>
        configuration.GetSection("PublicRooms:RoomIds").Get<int[]>() ?? [];

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken) =>
        View(await rooms.ListAsync(AllowedRoomIds, cancellationToken));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> ChiTiet(int id, CancellationToken cancellationToken)
    {
        var room = await rooms.GetAsync(id, AllowedRoomIds, cancellationToken);
        return room is null ? NotFound() : View(room);
    }
}
