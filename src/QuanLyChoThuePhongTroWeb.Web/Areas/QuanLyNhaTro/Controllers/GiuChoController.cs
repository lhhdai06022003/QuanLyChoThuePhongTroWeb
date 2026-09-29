using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using QuanLyChoThuePhongTroWeb.Application.Features.GiuChos.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.GiuChos.Services;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Controllers;

public sealed class GiuChoController(IGiuChoService reservations) : AdminBaseController
{
    [HttpGet]
    public async Task<IActionResult> Index() => View(await reservations.GetQueueAsync(ActorId));

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Duyet(int id,
        DateTime hanThanhToan, DateTime hanKyHopDong)
    {
        if (hanThanhToan.Year < 2000 || hanKyHopDong.Year < 2000)
        {
            TempData["ReservationError"] = "Vui lòng nhập đầy đủ thời hạn.";
            return RedirectToAction(nameof(Index));
        }
        var paymentDue = InVietnamTime(hanThanhToan);
        var signingDue = InVietnamTime(hanKyHopDong);
        var result = await reservations.ApproveAsync(id,
            new ApproveReservationRequest(paymentDue, signingDue), ActorId);
        TempData[result.Success ? "ReservationSuccess" : "ReservationError"] = result.Message;
        return RedirectToAction(nameof(Index));
    }

    private int ActorId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> KetThucChuaThanhToan(int id, string lyDo)
    {
        var result = await reservations.CloseUnpaidAsync(id, ActorId, lyDo);
        TempData[result.Success ? "ReservationSuccess" : "ReservationError"] = result.Message;
        return RedirectToAction(nameof(Index));
    }

    private static DateTimeOffset InVietnamTime(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Unspecified), TimeSpan.FromHours(7));
}
