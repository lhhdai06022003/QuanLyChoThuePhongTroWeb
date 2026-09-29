using Microsoft.AspNetCore.Mvc;
using QuanLyChoThuePhongTroWeb.Application.Features.PublicRooms;
using QuanLyChoThuePhongTroWeb.ViewModels.PublicRooms;

namespace QuanLyChoThuePhongTroWeb.Controllers;

[Route("phong")]
public sealed class PhongController(IPublicRoomService rooms, IConfiguration configuration,
    IWebHostEnvironment environment) : Controller
{
    private const int PageSize = 9;

    private int[] AllowedRoomIds =>
        configuration.GetSection("PublicRooms:RoomIds").Get<int[]>() ?? [];

    [HttpGet("")]
    public async Task<IActionResult> Index(double? giaTu, double? giaDen,
        double? dienTichTu, int page = 1, CancellationToken cancellationToken = default)
    {
        var available = await rooms.ListAsync(AllowedRoomIds, cancellationToken);
        var filtered = available
            .Where(room => (!giaTu.HasValue || room.GiaThue >= giaTu.Value) &&
                (!giaDen.HasValue || room.GiaThue <= giaDen.Value) &&
                (!dienTichTu.HasValue || room.DienTich >= dienTichTu.Value))
            .ToArray();
        var safePage = Math.Clamp(page, 1, Math.Max(1, (filtered.Length + PageSize - 1) / PageSize));
        var items = filtered.Skip((safePage - 1) * PageSize).Take(PageSize)
            .Select(room => new PublicRoomCardModel(room, GetImages(room.PhongTroId)))
            .ToArray();

        return View(new PublicRoomListModel(items, filtered.Length, safePage, PageSize,
            giaTu, giaDen, dienTichTu));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> ChiTiet(int id, CancellationToken cancellationToken)
    {
        var room = await rooms.GetAsync(id, AllowedRoomIds, cancellationToken);
        return room is null ? NotFound() : View(new PublicRoomCardModel(room, GetImages(id)));
    }

    private IReadOnlyList<string> GetImages(int roomId)
    {
        var directory = Path.Combine(environment.WebRootPath, "uploads", "rooms", roomId.ToString());
        if (!Directory.Exists(directory))
            return [];

        return Directory.EnumerateFiles(directory)
            .Where(path => new[] { ".jpg", ".jpeg", ".png", ".webp" }
                .Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .Select(path => $"/uploads/rooms/{roomId}/{Uri.EscapeDataString(Path.GetFileName(path))}")
            .ToArray();
    }
}
