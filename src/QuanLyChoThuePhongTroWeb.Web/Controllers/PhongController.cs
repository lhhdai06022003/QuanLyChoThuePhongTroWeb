using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuanLyChoThuePhongTroWeb.Application.Features.PublicRooms;
using QuanLyChoThuePhongTroWeb.Application.Features.Viewings;
using QuanLyChoThuePhongTroWeb.Application.Features.Reservations;
using QuanLyChoThuePhongTroWeb.Application.Features.ReservationPayments;
using QuanLyChoThuePhongTroWeb.Application.Features.ReservationSettlement;
using QuanLyChoThuePhongTroWeb.ViewModels.PublicRooms;

namespace QuanLyChoThuePhongTroWeb.Controllers;

[Route("phong")]
public sealed class PhongController(IPublicRoomService rooms, IViewingRequestService viewings,
    IReservationService reservations,
    IReservationPaymentService payments,
    IReservationSettlementService settlements,
    IConfiguration configuration,
    IWebHostEnvironment environment) : Controller
{
    private const int PageSize = 9;

    private int[]? AllowedRoomIds =>
        configuration.GetSection("PublicRooms:RoomIds").Get<int[]>();

    [HttpGet("")]
    public async Task<IActionResult> Index(decimal? giaTu, decimal? giaDen,
        double? dienTichTu, string? khuVuc, int page = 1,
        CancellationToken cancellationToken = default)
    {
        var area = string.IsNullOrWhiteSpace(khuVuc) ? null : khuVuc.Trim();
        var available = await rooms.ListAsync(AllowedRoomIds, cancellationToken);
        var filtered = available
            .Where(room => (!giaTu.HasValue || room.GiaThue >= giaTu.Value) &&
                (!giaDen.HasValue || room.GiaThue <= giaDen.Value) &&
                (!dienTichTu.HasValue || room.DienTich >= dienTichTu.Value) &&
                PublicRoomAreaSearch.Matches(room, area))
            .ToArray();
        var safePage = Math.Clamp(page, 1, Math.Max(1, (filtered.Length + PageSize - 1) / PageSize));
        var items = filtered.Skip((safePage - 1) * PageSize).Take(PageSize)
            .Select(room => new PublicRoomCardModel(room, GetImages(room)))
            .ToArray();

        return View(new PublicRoomListModel(items, filtered.Length, safePage, PageSize,
            giaTu, giaDen, dienTichTu, area));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> ChiTiet(int id, CancellationToken cancellationToken)
    {
        var room = await rooms.GetAsync(id, AllowedRoomIds, cancellationToken);
        return room is null ? NotFound() : View(new PublicRoomCardModel(room, GetImages(room)));
    }

    [Authorize(Roles = "KhachVangLai,KhachThue")]
    [HttpGet("{id:int}/hen-xem")]
    public async Task<IActionResult> HenXem(int id, CancellationToken cancellationToken)
    {
        var room = await rooms.GetAsync(id, AllowedRoomIds, cancellationToken);
        return room is null ? NotFound() : View(new ViewingPageModel
        {
            Room = new PublicRoomCardModel(room, GetImages(room)),
            Slots = await viewings.ListAvailableSlotsAsync(id, cancellationToken)
        });
    }

    [Authorize(Roles = "KhachVangLai,KhachThue")]
    [HttpPost("{id:int}/hen-xem")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> HenXem(int id, ViewingPageModel model,
        CancellationToken cancellationToken)
    {
        var room = await rooms.GetAsync(id, AllowedRoomIds, cancellationToken);
        if (room is null)
            return NotFound();

        model.Room = new PublicRoomCardModel(room, GetImages(room));
        model.Slots = await viewings.ListAvailableSlotsAsync(id, cancellationToken);
        if (!ModelState.IsValid)
            return View(model);

        DateTime? preferredUtc = null;
        if (model.PreferredTimeLocal.HasValue)
        {
            var local = DateTime.SpecifyKind(model.PreferredTimeLocal.Value,
                DateTimeKind.Unspecified);
            preferredUtc = new DateTimeOffset(local, TimeSpan.FromHours(7)).UtcDateTime;
        }

        var actorId = int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
            out var idNguoiDung) ? idNguoiDung : 0;
        var result = await viewings.SubmitAsync(actorId, new ViewingRequestInput(
            id, model.SlotId, preferredUtc, model.FullName, model.Phone,
            model.Email, model.Note), cancellationToken);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Message);
            return View(model);
        }

        TempData["GuestViewingMessage"] = "Yêu cầu xem phòng đã được ghi nhận.";
        return Redirect("/tai-khoan/yeu-cau-phong");
    }

    [Authorize(Roles = "KhachVangLai,KhachThue")]
    [HttpGet("{id:int}/giu-cho")]
    public async Task<IActionResult> GiuCho(int id, int? viewingId,
        CancellationToken cancellationToken)
    {
        var room = await rooms.GetAsync(id, AllowedRoomIds, cancellationToken);
        return room is null ? NotFound() : View(new ReservationPageModel(
            new PublicRoomCardModel(room, GetImages(room)), viewingId));
    }

    [Authorize(Roles = "KhachVangLai,KhachThue")]
    [HttpPost("{id:int}/giu-cho")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GuiYeuCauGiuCho(int id, int? viewingId,
        CancellationToken cancellationToken)
    {
        var actorId = int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
            out var idNguoiDung) ? idNguoiDung : 0;
        var result = await reservations.RequestAsync(actorId, id, viewingId, cancellationToken);
        if (result.Success)
        {
            TempData["GuestReservationMessage"] = result.Message;
            return Redirect("/tai-khoan/yeu-cau-phong");
        }

        var room = await rooms.GetAsync(id, AllowedRoomIds, cancellationToken);
        if (room is null)
            return NotFound();
        ModelState.AddModelError(string.Empty, result.Message);
        return View("GiuCho", new ReservationPageModel(new PublicRoomCardModel(room, GetImages(room)),
            viewingId));
    }

    [Authorize(Roles = "KhachVangLai,KhachThue")]
    [HttpGet("/tai-khoan/yeu-cau-phong")]
    public async Task<IActionResult> YeuCauCuaToi(CancellationToken cancellationToken)
    {
        var actorId = int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
            out var idNguoiDung) ? idNguoiDung : 0;
        return View(new GuestRequestsPageModel(
            await viewings.ListMineAsync(actorId, cancellationToken),
            await reservations.ListMineAsync(actorId, cancellationToken),
            await payments.ListMineAsync(actorId, cancellationToken),
            await settlements.ListMineAsync(actorId, cancellationToken),
            GetBankInfo()));
    }

    [Authorize(Roles = "KhachVangLai,KhachThue")]
    [HttpGet("/tai-khoan/yeu-cau-phong/giu-cho/{reservationId:int}/dat-coc")]
    public async Task<IActionResult> DatCoc(int reservationId,
        CancellationToken cancellationToken)
    {
        var actorId = int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
            out var userId) ? userId : 0;
        var reservation = (await reservations.ListMineAsync(actorId, cancellationToken))
            .SingleOrDefault(item => item.Id == reservationId);
        if (reservation is null)
            return NotFound();
        var evidence = (await payments.ListMineAsync(actorId, cancellationToken))
            .Where(item => item.PaymentRequestId == reservation.PaymentRequestId).ToArray();
        var settlement = (await settlements.ListMineAsync(actorId, cancellationToken))
            .SingleOrDefault(item => item.ReservationId == reservationId);
        return View(new GuestDepositPageModel(reservation, evidence,
            settlement, GetBankInfo()));
    }

    private GuestBankInfo? GetBankInfo()
    {
        var bank = configuration.GetSection("VietQRSettings")
            .Get<QuanLyChoThuePhongTroWeb.Application.Common.Configurations.VietQrSettings>();
        return bank is not null &&
            !string.IsNullOrWhiteSpace(bank.BankId) &&
            !string.IsNullOrWhiteSpace(bank.AccountNumber) &&
            !string.IsNullOrWhiteSpace(bank.AccountName) &&
            !bank.BankId.StartsWith('<') && !bank.AccountNumber.StartsWith('<') &&
            !bank.AccountName.StartsWith('<')
            ? new GuestBankInfo(bank.BankId, bank.AccountNumber, bank.AccountName)
            : null;
    }

    [Authorize(Roles = "KhachVangLai,KhachThue")]
    [HttpPost("/tai-khoan/yeu-cau-phong/lich-xem/{requestId:int}/huy")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> HuyLichXem(int requestId,
        CancellationToken cancellationToken)
    {
        var actorId = int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
            out var idNguoiDung) ? idNguoiDung : 0;
        var result = await viewings.CancelAsync(actorId, requestId, cancellationToken);
        TempData["GuestViewingMessage"] = result.Message;
        return Redirect("/tai-khoan/yeu-cau-phong");
    }

    [Authorize(Roles = "KhachVangLai,KhachThue")]
    [HttpPost("/tai-khoan/yeu-cau-phong/giu-cho/{requestId:int}/huy")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> HuyGiuCho(int requestId,
        CancellationToken cancellationToken)
    {
        var actorId = int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
            out var idNguoiDung) ? idNguoiDung : 0;
        var result = await reservations.CancelAsync(actorId, requestId, cancellationToken);
        TempData["GuestReservationMessage"] = result.Message;
        return Redirect("/tai-khoan/yeu-cau-phong");
    }

    private IReadOnlyList<string> GetImages(PublicRoomDto room)
    {
        if (!configuration.GetValue<bool>("PublicRooms:LegacySchemaPreview"))
            return room.ImageUrls ?? [];

        var directory = Path.Combine(environment.WebRootPath, "uploads", "rooms",
            room.PhongTroId.ToString());
        if (!Directory.Exists(directory))
            return [];

        return Directory.EnumerateFiles(directory)
            .Where(path => new[] { ".jpg", ".jpeg", ".png", ".webp" }
                .Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .Select(path => $"/uploads/rooms/{room.PhongTroId}/{Uri.EscapeDataString(Path.GetFileName(path))}")
            .ToArray();
    }
}
