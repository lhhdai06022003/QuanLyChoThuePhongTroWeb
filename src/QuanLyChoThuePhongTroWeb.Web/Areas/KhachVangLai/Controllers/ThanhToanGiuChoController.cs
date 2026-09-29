using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Services;
using QuanLyChoThuePhongTroWeb.Application.Common.Configurations;
using QuanLyChoThuePhongTroWeb.Application.Common.Files;
using QuanLyChoThuePhongTroWeb.Application.Features.GiuChos.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.GiuChos.Services;

namespace QuanLyChoThuePhongTroWeb.Areas.KhachVangLai.Controllers;

[Area("KhachVangLai")]
[Authorize]
[Route("thanh-toan-giu-cho")]
public sealed class ThanhToanGiuChoController(IThanhToanGiuChoService payments,
    VietQrSettings bank, IVietQRService vietQr) : Controller
{
    [HttpGet("{reservationId:int}")]
    public async Task<IActionResult> ChiTiet(int reservationId)
    {
        var payment = await payments.GetForGuestAsync(reservationId, ActorId);
        if (payment is null) return NotFound();
        var configured = !string.IsNullOrWhiteSpace(bank.BankId) &&
            !string.IsNullOrWhiteSpace(bank.AccountNumber) &&
            !string.IsNullOrWhiteSpace(bank.AccountName) &&
            !bank.AccountNumber.Contains('<') && !bank.AccountName.Contains('<');
        ViewBag.BankConfigured = configured;
        if (configured)
        {
            ViewBag.BankId = bank.BankId;
            ViewBag.AccountNumber = bank.AccountNumber;
            ViewBag.AccountName = bank.AccountName;
            var payload = vietQr.GenerateVietQRString(bank.BankId, bank.AccountNumber,
                payment.SoTien, payment.NoiDungChuyenKhoan);
            ViewBag.QrImage = $"data:image/png;base64,{Convert.ToBase64String(vietQr.GenerateQRCodePNGBytes(payload))}";
        }
        return View(payment);
    }

    [HttpPost("{reservationId:int}/minh-chung")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(6_000_000)]
    public async Task<IActionResult> GuiMinhChung(int reservationId, int paymentId,
        decimal soTienKhaiBao, DateTime ngayChuyenTien, string? maGiaoDichNganHang,
        string? ghiChu, IFormFile? image)
    {
        var payment = await payments.GetForGuestAsync(reservationId, ActorId);
        if (payment is null || payment.Id != paymentId) return NotFound();
        if (image is null || ngayChuyenTien.Year < 2000)
        {
            TempData["PaymentError"] = "Vui lòng chọn ảnh và thời điểm chuyển tiền.";
            return RedirectToAction(nameof(ChiTiet), new { reservationId });
        }

        var localTime = new DateTimeOffset(DateTime.SpecifyKind(ngayChuyenTien,
            DateTimeKind.Unspecified), TimeSpan.FromHours(7));
        await using var stream = image.OpenReadStream();
        var result = await payments.SubmitProofAsync(paymentId, ActorId,
            new SubmitProofRequest(soTienKhaiBao, localTime, maGiaoDichNganHang, ghiChu),
            new UploadFile(stream, image.FileName, image.ContentType, image.Length));
        TempData[result.Success ? "PaymentSuccess" : "PaymentError"] = result.Message;
        return RedirectToAction(nameof(ChiTiet), new { reservationId });
    }

    private int ActorId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
}
