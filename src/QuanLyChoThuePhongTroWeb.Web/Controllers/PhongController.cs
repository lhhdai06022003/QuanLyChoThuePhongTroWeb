using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Services;
using QuanLyChoThuePhongTroWeb.Application.Common.Configurations;
using QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;
using QuanLyChoThuePhongTroWeb.Application.Features.PublicRooms;
using QuanLyChoThuePhongTroWeb.Application.Features.Viewings;
using QuanLyChoThuePhongTroWeb.Application.Features.Reservations;
using QuanLyChoThuePhongTroWeb.Application.Features.ReservationPayments;
using QuanLyChoThuePhongTroWeb.Application.Features.ReservationSettlement;
using QuanLyChoThuePhongTroWeb.ViewModels.PublicRooms;
using QuanLyChoThuePhongTroWeb.Web.Helpers;

namespace QuanLyChoThuePhongTroWeb.Controllers;

[Route("phong")]
public sealed class PhongController(
    IPublicRoomService rooms,
    IViewingRequestService viewings,
    IReservationService reservations,
    IReservationPaymentService payments,
    IReservationSettlementService settlements,
    IConfiguration configuration,
    IReservationVnPayService reservationVnPay,
    VnPayOptions vnPayOptions,
    IVietQRService vietQrService) : Controller
{
    private const int PageSize = 9;

    private int[]? AllowedRoomIds =>
        configuration.GetSection("PublicRooms:RoomIds").Get<int[]>();

    [HttpGet("")]
    public async Task<IActionResult> Index(decimal? giaTu, decimal? giaDen,
        double? dienTichTu, string? khuVuc, int page = 1, int? soNguoi = null, string? sapXep = null,
        CancellationToken cancellationToken = default)
    {
        var search = PublicRoomDiscovery.Normalize(new(giaTu, giaDen, dienTichTu, khuVuc,
            soNguoi, sapXep ?? "mac-dinh"));
        var available = await rooms.ListAsync(AllowedRoomIds, cancellationToken);
        var filtered = PublicRoomDiscovery.Apply(available, search);
        var safePage = Math.Clamp(page, 1, Math.Max(1, (filtered.Count + PageSize - 1) / PageSize));
        var items = filtered.Skip((safePage - 1) * PageSize).Take(PageSize)
            .Select(room => new PublicRoomCardModel(room, GetImages(room)))
            .ToArray();

        var areas = available.GroupBy(room => room.TenChiNhanh)
            .Select(group => new PublicRoomAreaOption(group.Key, group.Count(), group.Min(room => room.GiaThue)))
            .OrderByDescending(area => area.Count).ThenBy(area => area.Name).Take(8).ToArray();
        var hero = available.Select(room => new PublicRoomCardModel(room, GetImages(room)))
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
                people = room.SoNguoiToiDa, floor = room.TangLau, image = GetImages(room).FirstOrDefault()
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
            (_, room) => new PublicRoomCardModel(room, GetImages(room))).ToArray();
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
        return room is null ? NotFound() : View(new PublicRoomCardModel(room, GetImages(room)));
    }

    [AllowAnonymous]
    [HttpGet("{id:int}/hen-xem")]
    public async Task<IActionResult> HenXem(int id, CancellationToken cancellationToken)
    {
        var room = await rooms.GetAsync(id, AllowedRoomIds, cancellationToken);
        if (room is null) return NotFound();
        if (User.Identity?.IsAuthenticated != true)
            return LoginPrompt(room, "hẹn xem phòng");
        if (!User.IsInRole("KhachVangLai") && !User.IsInRole("KhachThue"))
            return Forbid();

        return View(new ViewingPageModel
        {
            Room = new PublicRoomCardModel(room, GetImages(room))
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
            id, null, preferredUtc, model.FullName, model.Phone,
            model.Email, model.Note), cancellationToken);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Message);
            return View(model);
        }

        TempData["GuestViewingMessage"] = "Đã gửi thời gian mong muốn. Nhân viên sẽ gọi số điện thoại bạn cung cấp để xác nhận lịch xem.";
        return Redirect("/tai-khoan/yeu-cau-phong");
    }

    [AllowAnonymous]
    [HttpGet("{id:int}/giu-cho")]
    public async Task<IActionResult> GiuCho(int id, int? viewingId,
        CancellationToken cancellationToken)
    {
        var room = await rooms.GetAsync(id, AllowedRoomIds, cancellationToken);
        if (room is null) return NotFound();
        if (User.Identity?.IsAuthenticated != true)
            return LoginPrompt(room, "đặt phòng");
        if (!User.IsInRole("KhachVangLai") && !User.IsInRole("KhachThue"))
            return Forbid();

        return View(new ReservationPageModel(
            new PublicRoomCardModel(room, GetImages(room)), viewingId));
    }

    private IActionResult LoginPrompt(PublicRoomDto room, string actionName) =>
        View("CanDangNhap", new PublicRoomLoginPromptModel(
            new PublicRoomCardModel(room, GetImages(room)), actionName,
            Request.PathBase.ToString() + Request.Path + Request.QueryString));

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

        var bankSettings = configuration.GetSection("VietQRSettings")
            .Get<VietQrSettings>();
        var bankInfo = GetBankInfo();
        var remaining = Math.Max(0, (reservation.HoldAmount ?? 0) - reservation.ConfirmedAmount);

        IReadOnlyList<BankDeeplinkDto>? deeplinks = null;
        if (bankSettings != null && bankInfo != null && remaining > 0 && !string.IsNullOrWhiteSpace(reservation.PaymentCode))
        {
            deeplinks = VietQrDeeplinks.BuildAll(bankSettings, remaining, reservation.PaymentCode);
        }

        return View(new GuestDepositPageModel(
            reservation,
            evidence,
            settlement,
            bankInfo,
            vnPayOptions.Enabled,
            deeplinks));
    }

    [Authorize(Roles = "KhachVangLai,KhachThue")]
    [HttpGet("/tai-khoan/yeu-cau-phong/giu-cho/{reservationId:int}/dat-coc/qr")]
    public async Task<IActionResult> DatCocQr(int reservationId, [FromQuery] bool taiVe, CancellationToken cancellationToken)
    {
        var actorId = int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
            out var idNguoiDung) ? idNguoiDung : 0;
        if (actorId <= 0)
        {
            return Unauthorized();
        }

        var reservation = (await reservations.ListMineAsync(actorId, cancellationToken))
            .SingleOrDefault(item => item.Id == reservationId);
        if (reservation is null)
        {
            return NotFound();
        }

        var bank = configuration.GetSection("VietQRSettings")
            .Get<VietQrSettings>();
        if (bank is null || string.IsNullOrWhiteSpace(bank.BankId) || string.IsNullOrWhiteSpace(bank.AccountNumber) ||
            bank.BankId.StartsWith('<') || bank.AccountNumber.StartsWith('<'))
        {
            return NotFound();
        }

        var remaining = Math.Max(0, (reservation.HoldAmount ?? 0) - reservation.ConfirmedAmount);
        var memo = reservation.PaymentCode ?? $"TTPTGC{reservation.Id}";
        var payload = vietQrService.GenerateVietQRString(bank.BankId, bank.AccountNumber, remaining, memo);
        var bytes = vietQrService.GenerateQRCodePNGBytes(payload);
        var fileName = $"vietqr-dat-coc-phong-{reservation.RoomNumber}.png";

        return taiVe ? File(bytes, "image/png", fileName) : File(bytes, "image/png");
    }

    [Authorize(Roles = "KhachVangLai,KhachThue")]
    [HttpPost("/tai-khoan/yeu-cau-phong/giu-cho/{reservationId:int}/dat-coc/vnpay")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DatCocVnPay(int reservationId, CancellationToken cancellationToken)
    {
        if (!vnPayOptions.Enabled)
        {
            return NotFound();
        }

        var actorId = int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
            out var idNguoiDung) ? idNguoiDung : 0;
        if (actorId <= 0)
        {
            return Unauthorized(new { success = false, message = "Bạn cần đăng nhập để thực hiện thanh toán." });
        }

        var clientIp = PaymentWebHelper.ResolveClientIp(HttpContext);
        var returnUrl = PaymentWebHelper.BuildAbsoluteUrl(Request, "/tai-khoan/yeu-cau-phong/giu-cho/dat-coc/vnpay/ket-qua");

        var result = await reservationVnPay.CreatePaymentUrlAsync(reservationId, actorId, clientIp, returnUrl, cancellationToken);
        return result.Success
            ? Ok(new { success = true, url = result.Data })
            : this.ToErrorResult(result);
    }

    [Authorize(Roles = "KhachVangLai,KhachThue")]
    [HttpGet("/tai-khoan/yeu-cau-phong/giu-cho/dat-coc/vnpay/ket-qua")]
    public async Task<IActionResult> DatCocVnPayKetQua(CancellationToken cancellationToken)
    {
        if (!vnPayOptions.Enabled)
        {
            return NotFound();
        }

        var actorId = int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
            out var idNguoiDung) ? idNguoiDung : 0;
        if (actorId <= 0)
        {
            return View("DatCocVnPayKetQua", ReservationVnPayKetQuaViewModel.From(ReservationVnPayReturnResult.Unverified));
        }

        var query = Request.Query.ToDictionary(q => q.Key, q => q.Value.ToString(), StringComparer.Ordinal);
        var result = await reservationVnPay.GetReturnResultAsync(query, actorId, cancellationToken);
        return View("DatCocVnPayKetQua", ReservationVnPayKetQuaViewModel.From(result));
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

    // Ảnh phòng lấy từ AnhPhongTro (URL Cloudinary), không đọc tệp trong source.
    private static IReadOnlyList<string> GetImages(PublicRoomDto room) =>
        room.ImageUrls ?? [];
}
