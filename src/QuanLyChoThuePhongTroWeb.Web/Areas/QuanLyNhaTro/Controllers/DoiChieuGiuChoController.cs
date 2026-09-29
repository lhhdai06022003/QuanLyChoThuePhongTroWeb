using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using QuanLyChoThuePhongTroWeb.Application.Features.GiuChos.Services;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Controllers;

public sealed class DoiChieuGiuChoController(IThanhToanGiuChoService payments) : AdminBaseController
{
    [HttpGet]
    public async Task<IActionResult> Index() => View(await payments.GetReviewQueueAsync(ActorId));

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> XacNhan(int id, string maGiaoDich,
        decimal soTienThucNhan, DateTime ngayThucNhan)
    {
        if (!ModelState.IsValid || ngayThucNhan.Year < 2000) return RedirectWithError("Vui lòng nhập thông tin và thời điểm thực nhận hợp lệ.");
        var result = await payments.ConfirmAsync(id, ActorId, maGiaoDich,
            soTienThucNhan, InVietnamTime(ngayThucNhan));
        TempData[result.Success ? "ReviewSuccess" : "ReviewError"] = result.Message;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TuChoi(int id, string lyDo, decimal soTienThucNhan = 0,
        string? maGiaoDich = null, DateTime? ngayThucNhan = null)
    {
        if (!ModelState.IsValid) return RedirectWithError("Số tiền hoặc thông tin nhập không hợp lệ.");
        if (soTienThucNhan > 0 && (ngayThucNhan is null || ngayThucNhan.Value.Year < 2000))
            return RedirectWithError("Có tiền thực nhận thì cần nhập thời điểm nhận.");
        var result = await payments.RejectAsync(id, ActorId, lyDo,
            soTienThucNhan, maGiaoDich,
            ngayThucNhan is null ? null : InVietnamTime(ngayThucNhan.Value));
        TempData[result.Success ? "ReviewSuccess" : "ReviewError"] = result.Message;
        return RedirectToAction(nameof(Index));
    }

    private RedirectToActionResult RedirectWithError(string message)
    {
        TempData["ReviewError"] = message;
        return RedirectToAction(nameof(Index))!;
    }

    private int ActorId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
    private static DateTimeOffset InVietnamTime(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Unspecified), TimeSpan.FromHours(7));
}
