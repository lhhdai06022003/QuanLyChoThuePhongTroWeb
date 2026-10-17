using Microsoft.AspNetCore.Authorization;
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
        double? dienTichTu, string? khuVuc, int page = 1, int? soNguoi = null, string? sapXep = null,
        CancellationToken cancellationToken = default)
    {
        var search = PublicRoomDiscovery.Normalize(new(giaTu, giaDen, dienTichTu, khuVuc,
            soNguoi, sapXep ?? "mac-dinh"));
        var available = await rooms.ListAsync(AllowedRoomIds, cancellationToken);
        var filtered = PublicRoomDiscovery.Apply(available, search);
        var safePage = Math.Clamp(page, 1, Math.Max(1, (filtered.Count + PageSize - 1) / PageSize));
        var items = filtered.Skip((safePage - 1) * PageSize).Take(PageSize)
            .Select(room => new PublicRoomCardModel(room, GetImages(room.PhongTroId)))
            .ToArray();

        var areas = available.GroupBy(room => room.TenChiNhanh)
            .Select(group => new PublicRoomAreaOption(group.Key, group.Count(), group.Min(room => room.GiaThue)))
            .OrderByDescending(area => area.Count).ThenBy(area => area.Name).Take(8).ToArray();
        var hero = available.Select(room => new PublicRoomCardModel(room, GetImages(room.PhongTroId)))
            .FirstOrDefault(room => room.Images.Count > 0);
        return View(new PublicRoomListModel(items, filtered.Count, safePage, PageSize,
            search.MinPrice, search.MaxPrice, search.MinArea, search.Area, search.Occupants,
            search.Sort, areas, hero));
    }

    [HttpGet("da-luu")]
    public IActionResult DaLuu() => View();

    [HttpGet("cam-nang")]
    public IActionResult CamNang() => View();

    [HttpGet("du-lieu")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> DuLieu(string? ids, CancellationToken cancellationToken)
    {
        var selected = ParseRoomIds(ids, 50);
        var available = selected.Length == 0 ? [] : await rooms.ListAsync(AllowedRoomIds, cancellationToken);
        var found = selected.Join(available, id => id, room => room.PhongTroId, (_, room) => room).ToArray();
        return Json(new
        {
            rooms = found.Select(room => new
            {
                id = room.PhongTroId, number = room.SoPhong, branch = room.TenChiNhanh,
                address = room.DiaChi, price = room.GiaThue, area = room.DienTich,
                people = room.SoNguoiToiDa, floor = room.TangLau, image = GetImages(room.PhongTroId).FirstOrDefault()
            }),
            unavailableIds = selected.Except(found.Select(room => room.PhongTroId))
        });
    }

    [HttpGet("so-sanh")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> SoSanh(string? ids, CancellationToken cancellationToken)
    {
        var selected = ParseRoomIds(ids, 3);
        var available = selected.Length == 0 ? [] : await rooms.ListAsync(AllowedRoomIds, cancellationToken);
        var found = selected.Join(available, id => id, room => room.PhongTroId,
            (_, room) => new PublicRoomCardModel(room, GetImages(room.PhongTroId))).ToArray();
        return View(new PublicRoomComparisonModel(found, selected.Length - found.Length));
    }

    private static int[] ParseRoomIds(string? value, int limit) =>
        string.IsNullOrWhiteSpace(value) || value.Length > 1000 ? [] : value.Split(',')
            .Select(token => int.TryParse(token, out var id) && id > 0 ? id : 0)
            .Where(id => id > 0).Distinct().Take(limit).ToArray();

    [HttpGet("{id:int}")]
    public async Task<IActionResult> ChiTiet(int id, CancellationToken cancellationToken)
    {
        var room = await rooms.GetAsync(id, AllowedRoomIds, cancellationToken);
        return room is null ? NotFound() : View(new PublicRoomCardModel(room, GetImages(room.PhongTroId)));
    }

    [AllowAnonymous]
    [HttpGet("{id:int}/hen-xem")]
    public async Task<IActionResult> HenXem(int id, CancellationToken cancellationToken)
    {
        var room = await rooms.GetAsync(id, AllowedRoomIds, cancellationToken);
        if (room is null) return NotFound();
        if (User.Identity?.IsAuthenticated != true)
            return View("CanDangNhap", new PublicRoomLoginPromptModel(
                new PublicRoomCardModel(room, GetImages(id)), "hẹn xem phòng",
                Request.PathBase.ToString() + Request.Path + Request.QueryString));
        return View(new PublicRoomCardModel(room, GetImages(id)));
    }

    [AllowAnonymous]
    [HttpGet("{id:int}/giu-cho")]
    public async Task<IActionResult> GiuCho(int id, CancellationToken cancellationToken)
    {
        var room = await rooms.GetAsync(id, AllowedRoomIds, cancellationToken);
        if (room is null) return NotFound();
        if (User.Identity?.IsAuthenticated != true)
            return View("CanDangNhap", new PublicRoomLoginPromptModel(
                new PublicRoomCardModel(room, GetImages(id)), "đặt phòng",
                Request.PathBase.ToString() + Request.Path + Request.QueryString));
        return View(new PublicRoomCardModel(room, GetImages(id)));
    }

    [Authorize]
    [HttpGet("/tai-khoan/yeu-cau-phong")]
    public IActionResult YeuCauCuaToi() => View();

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
