using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuanLyChoThuePhongTroWeb.Application.Features.ReservationPayments;
using QuanLyChoThuePhongTroWeb.Services;

namespace QuanLyChoThuePhongTroWeb.Controllers;

[Authorize]
[Route("tai-khoan/yeu-cau-phong/minh-chung")]
public sealed class ReservationPaymentController(
    IReservationPaymentService payments, PrivateHoldEvidenceFiles files) : Controller
{
    private int ActorId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier),
        out var id) ? id : 0;

    [Authorize(Roles = "KhachVangLai")]
    [HttpPost("")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> Upload(int paymentRequestId, decimal claimedAmount,
        DateTime transferTimeLocal, string? bankReference, string? note,
        IFormFile? image, CancellationToken cancellationToken)
    {
        if (image is null || transferTimeLocal == default)
        {
            TempData["GuestReservationMessage"] = "Cần chọn ảnh và thời gian chuyển tiền.";
            return Redirect("/tai-khoan/yeu-cau-phong");
        }

        string fileName;
        try
        {
            fileName = await files.SaveAsync(image, cancellationToken);
        }
        catch (InvalidDataException ex)
        {
            TempData["GuestReservationMessage"] = ex.Message;
            return Redirect("/tai-khoan/yeu-cau-phong");
        }

        try
        {
            var transferUtc = new DateTimeOffset(
                DateTime.SpecifyKind(transferTimeLocal, DateTimeKind.Unspecified),
                TimeSpan.FromHours(7)).UtcDateTime;
            var result = await payments.SubmitEvidenceAsync(ActorId,
                new EvidenceSubmission(paymentRequestId, fileName, claimedAmount,
                    transferUtc, bankReference, note), cancellationToken);
            if (!result.Success)
                files.Delete(fileName);
            TempData["GuestReservationMessage"] = result.Message;
        }
        catch
        {
            files.Delete(fileName);
            throw;
        }
        return Redirect("/tai-khoan/yeu-cau-phong");
    }

    [HttpGet("{evidenceId:int}")]
    public async Task<IActionResult> Image(int evidenceId,
        CancellationToken cancellationToken)
    {
        var access = await payments.GetEvidenceAccessAsync(ActorId, evidenceId,
            cancellationToken);
        if (!access.Success || access.Data is null)
            return NotFound();
        var path = files.GetPath(access.Data.PrivateFileName);
        return path is null || !System.IO.File.Exists(path) ? NotFound() :
            PhysicalFile(path, PrivateHoldEvidenceFiles.ContentType(
                access.Data.PrivateFileName));
    }
}
