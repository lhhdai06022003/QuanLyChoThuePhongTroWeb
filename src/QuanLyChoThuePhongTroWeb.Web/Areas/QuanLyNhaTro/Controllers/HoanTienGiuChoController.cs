using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using QuanLyChoThuePhongTroWeb.Application.Features.GiuChos.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.GiuChos.Services;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Controllers;

public sealed class HoanTienGiuChoController(IHoanTienGiuChoService refunds) : AdminBaseController
{
    [HttpGet]
    public async Task<IActionResult> Index() => View(await refunds.GetQueueAsync(ActorId));

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Duyet(int id, decimal soTienHoanDuyet, string lyDo)
    {
        if (!ModelState.IsValid) return InvalidInput();
        var result = await refunds.DecideAsync(id,
            new RefundDecisionRequest(soTienHoanDuyet, lyDo), ActorId);
        TempData[result.Success ? "RefundSuccess" : "RefundError"] = result.Message;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GhiNhan(int id, string maGiaoDichHoan, decimal soTienHoan,
        DateTime ngayHoan, string? ghiChu)
    {
        if (!ModelState.IsValid || ngayHoan.Year < 2000)
        {
            TempData["RefundError"] = "Vui lòng nhập thời điểm chuyển hoàn.";
            return RedirectToAction(nameof(Index));
        }
        var when = new DateTimeOffset(DateTime.SpecifyKind(ngayHoan, DateTimeKind.Unspecified),
            TimeSpan.FromHours(7));
        var result = await refunds.RecordAsync(id,
            new RecordRefundRequest(maGiaoDichHoan, soTienHoan, when, ghiChu), ActorId);
        TempData[result.Success ? "RefundSuccess" : "RefundError"] = result.Message;
        return RedirectToAction(nameof(Index));
    }

    private int ActorId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
    private IActionResult InvalidInput()
    {
        TempData["RefundError"] = "Số tiền hoặc thông tin nhập không hợp lệ.";
        return RedirectToAction(nameof(Index));
    }
}
