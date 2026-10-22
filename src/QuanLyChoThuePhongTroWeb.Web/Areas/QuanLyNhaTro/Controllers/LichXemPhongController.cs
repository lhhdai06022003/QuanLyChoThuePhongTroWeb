using Microsoft.AspNetCore.Mvc;
using QuanLyChoThuePhongTroWeb.Application.Features.Viewings;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Controllers;

public sealed class LichXemPhongController(IViewingAdminRequestService requests) : AdminBaseController
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken) =>
        View(new ViewingAdminPageModel(await requests.ListAsync(CurrentActorId, cancellationToken)));

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
        TempData["ViewingAdminWarning"] = !result.Success || result.Data is ViewingEmailDelivery { Sent: false };
        return RedirectToAction(nameof(Index));
    }
    [HttpPost]
    public async Task<IActionResult> GuiLaiEmail(int requestId, CancellationToken cancellationToken)
    {
        var result = await requests.ResendNotificationAsync(CurrentActorId, requestId, cancellationToken);
        TempData["ViewingAdminMessage"] = result.Message;
        TempData["ViewingAdminWarning"] = !result.Success;
        return RedirectToAction(nameof(Index));
    }
}
