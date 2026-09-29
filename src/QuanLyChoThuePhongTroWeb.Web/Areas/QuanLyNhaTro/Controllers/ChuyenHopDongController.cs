using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using QuanLyChoThuePhongTroWeb.Application.Features.GiuChos.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.GiuChos.UseCases;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Controllers;

public sealed class ChuyenHopDongController(IChuyenHopDongTuGiuChoUseCase contracts) : AdminBaseController
{
    [HttpGet]
    public async Task<IActionResult> Tao(int id)
    {
        var candidate = await contracts.GetCandidateAsync(id, ActorId);
        return candidate is null ? NotFound() : View(candidate);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Tao(int id, DateTime ngayBatDau, DateTime? ngayKetThuc,
        int[]? dieuKhoanMauIds, bool xacNhanKyQuaHan)
    {
        if (!ModelState.IsValid || ngayBatDau.Year < 2000)
        {
            TempData["ContractError"] = "Vui lòng nhập ngày hợp đồng hợp lệ.";
            return RedirectToAction(nameof(Tao), new { id });
        }
        var result = await contracts.ExecuteAsync(id,
            new ReservationContractRequest(InVietnam(ngayBatDau),
                ngayKetThuc is DateTime end ? InVietnam(end) : null,
                dieuKhoanMauIds ?? [], xacNhanKyQuaHan), ActorId);
        if (!result.Success)
        {
            TempData["ContractError"] = result.Message;
            return RedirectToAction(nameof(Tao), new { id });
        }
        TempData["ReservationSuccess"] = result.Message;
        return RedirectToAction("Index", "GiuCho");
    }

    private int ActorId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
    private static DateTimeOffset InVietnam(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Unspecified), TimeSpan.FromHours(7));
}
