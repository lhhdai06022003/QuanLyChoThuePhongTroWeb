using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuanLyChoThuePhongTroWeb.Application.Common.Configurations;
using QuanLyChoThuePhongTroWeb.Application.Common.Files;
using QuanLyChoThuePhongTroWeb.Application.Features.ReservationPayments;

namespace QuanLyChoThuePhongTroWeb.Controllers;

[Authorize]
[Route("tai-khoan/yeu-cau-phong/minh-chung")]
public sealed class ReservationPaymentController(
    IReservationPaymentService payments, InvoicePaymentOptions options) : Controller
{
    private int ActorId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier),
        out var id) ? id : 0;

    [Authorize(Roles = "KhachVangLai,KhachThue")]
    [HttpPost("")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> Upload(int paymentRequestId, decimal claimedAmount,
        DateTime transferTimeLocal, string? bankReference, string? note,
        IFormFile? image, CancellationToken cancellationToken)
    {
        if (image is null || image.Length == 0 || transferTimeLocal == default)
        {
            TempData["GuestReservationMessage"] = "Cần chọn ảnh và thời gian chuyển tiền.";
            return Redirect("/tai-khoan/yeu-cau-phong");
        }

        if (image.Length > options.MinhChungToiDaBytes)
        {
            TempData["GuestReservationMessage"] =
                $"Ảnh vượt quá dung lượng cho phép ({options.MinhChungToiDaBytes / (1024 * 1024)} MB).";
            return Redirect("/tai-khoan/yeu-cau-phong");
        }

        await using var buffer = new MemoryStream();
        await image.CopyToAsync(buffer, cancellationToken);
        buffer.Position = 0;
        var upload = new UploadFile(buffer, Path.GetFileName(image.FileName),
            image.ContentType ?? string.Empty, buffer.Length);

        // Kiểm ở Web trước, Application kiểm lại.
        var fileError = ProofImageValidator.Validate(upload, options.MinhChungToiDaBytes);
        if (fileError is not null)
        {
            TempData["GuestReservationMessage"] = fileError;
            return Redirect("/tai-khoan/yeu-cau-phong");
        }

        var transferUtc = new DateTimeOffset(
            DateTime.SpecifyKind(transferTimeLocal, DateTimeKind.Unspecified),
            TimeSpan.FromHours(7)).UtcDateTime;
        var result = await payments.SubmitEvidenceAsync(ActorId,
            new EvidenceSubmission(paymentRequestId, upload, claimedAmount,
                transferUtc, bankReference, note), cancellationToken);
        TempData["GuestReservationMessage"] = result.Message;
        return Redirect("/tai-khoan/yeu-cau-phong");
    }

    [HttpGet("{evidenceId:int}")]
    public async Task<IActionResult> Image(int evidenceId,
        CancellationToken cancellationToken)
    {
        var result = await payments.GetEvidenceImageAsync(ActorId, evidenceId,
            cancellationToken);
        return result.Success && result.Data is not null
            ? File(result.Data.Bytes, result.Data.ContentType)
            : NotFound();
    }
}
