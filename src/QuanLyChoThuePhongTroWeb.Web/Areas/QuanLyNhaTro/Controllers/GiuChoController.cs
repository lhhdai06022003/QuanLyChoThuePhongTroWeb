using Microsoft.AspNetCore.Mvc;
using QuanLyChoThuePhongTroWeb.Application.Features.Reservations;
using QuanLyChoThuePhongTroWeb.Application.Features.ReservationPayments;
using QuanLyChoThuePhongTroWeb.Application.Features.ReservationSettlement;
using QuanLyChoThuePhongTroWeb.Application.Features.RoomDepositedNotices;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Controllers;

public sealed class GiuChoController(IReservationService reservations,
    IReservationPaymentService payments,
    IReservationSettlementService settlements,
    IRoomNoticeService notices) : AdminBaseController
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken) =>
        View(await reservations.ListForStaffAsync(CurrentActorId, cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Duyet(int reservationId, decimal amount,
        DateTime paymentDeadlineLocal, DateTime contractDeadlineLocal,
        CancellationToken cancellationToken)
    {
        var paymentUtc = new DateTimeOffset(
            DateTime.SpecifyKind(paymentDeadlineLocal, DateTimeKind.Unspecified),
            TimeSpan.FromHours(7)).UtcDateTime;
        var contractUtc = new DateTimeOffset(
            DateTime.SpecifyKind(contractDeadlineLocal, DateTimeKind.Unspecified),
            TimeSpan.FromHours(7)).UtcDateTime;
        var result = await reservations.ApproveAsync(CurrentActorId, reservationId,
            amount, paymentUtc, contractUtc, cancellationToken);
        TempData["ReservationAdminMessage"] = result.Message;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> GiaHanKyHopDong(int reservationId,
        DateTime newDeadlineLocal, CancellationToken cancellationToken)
    {
        var newDeadlineUtc = new DateTimeOffset(
            DateTime.SpecifyKind(newDeadlineLocal, DateTimeKind.Unspecified),
            TimeSpan.FromHours(7)).UtcDateTime;
        var result = await reservations.ExtendContractDeadlineAsync(CurrentActorId,
            reservationId, newDeadlineUtc, cancellationToken);
        TempData["ReservationAdminMessage"] = result.Message;
        return RedirectToAction(nameof(ChiTiet), new { id = reservationId });
    }

    [HttpPost]
    public async Task<IActionResult> HuyQuaHanKy(int reservationId, string reason,
        CancellationToken cancellationToken)
    {
        var result = await reservations.CancelExpiredContractAsync(CurrentActorId,
            reservationId, reason, cancellationToken);
        TempData["ReservationAdminMessage"] = result.Message;
        return RedirectToAction(nameof(ChiTiet), new { id = reservationId });
    }

    [HttpGet]
    public async Task<IActionResult> ChiTiet(int id, CancellationToken cancellationToken)
    {
        var reservation = (await reservations.ListForStaffAsync(CurrentActorId,
            cancellationToken)).SingleOrDefault(r => r.Id == id);
        if (reservation is null)
            return NotFound();
        return View(new ReservationAdminDetailModel(reservation,
            await payments.ListForReservationAsync(CurrentActorId, id,
                reservation.BranchId, cancellationToken),
            await settlements.GetForStaffAsync(CurrentActorId, id,
                cancellationToken)));
    }

    [HttpPost]
    public async Task<IActionResult> XacNhanTien(int reservationId,
        int paymentRequestId, int? evidenceId, string bankReference,
        decimal actualAmount, DateTime receivedAtLocal,
        CancellationToken cancellationToken)
    {
        var receivedAtUtc = new DateTimeOffset(
            DateTime.SpecifyKind(receivedAtLocal, DateTimeKind.Unspecified),
            TimeSpan.FromHours(7)).UtcDateTime;
        var result = await payments.ConfirmReceivedAsync(CurrentActorId,
            paymentRequestId, evidenceId, bankReference, actualAmount,
            receivedAtUtc,
            cancellationToken);
        TempData["ReservationAdminMessage"] = result.Message;
        return RedirectToAction(nameof(ChiTiet), new { id = reservationId });
    }

    [HttpPost]
    public async Task<IActionResult> ApDungCoc(int reservationId, int contractId,
        decimal amount, CancellationToken cancellationToken)
    {
        var result = await settlements.ApplyAsync(CurrentActorId, reservationId,
            contractId, amount, cancellationToken);
        TempData["ReservationAdminMessage"] = result.Message;
        return RedirectToAction(nameof(ChiTiet), new { id = reservationId });
    }

    [HttpPost]
    public async Task<IActionResult> QuyetDinhHoan(int reservationId,
        decimal amount, string reason, CancellationToken cancellationToken)
    {
        var result = await settlements.DecideRefundAsync(CurrentActorId,
            reservationId, amount, reason, cancellationToken);
        TempData["ReservationAdminMessage"] = result.Message;
        return RedirectToAction(nameof(ChiTiet), new { id = reservationId });
    }

    [HttpPost]
    public async Task<IActionResult> XacNhanHoan(int reservationId,
        decimal amount, string bankReference, CancellationToken cancellationToken)
    {
        var result = await settlements.RecordRefundAsync(CurrentActorId,
            reservationId, bankReference, amount, cancellationToken);
        TempData["ReservationAdminMessage"] = result.Message;
        return RedirectToAction(nameof(ChiTiet), new { id = reservationId });
    }

    [HttpGet]
    public async Task<IActionResult> ThongBaoDatCoc(int roomId,
        CancellationToken cancellationToken)
    {
        var result = await notices.PreviewAsync(CurrentActorId, roomId,
            cancellationToken);
        if (result.ErrorKind == QuanLyChoThuePhongTroWeb.Application.Common.Models.ServiceErrorKind.Forbidden)
            return Forbid();
        return result.Success && result.Data is not null
            ? View(new RoomNoticePageModel(result.Data))
            : NotFound(result.Message);
    }

    [HttpPost]
    public async Task<IActionResult> GuiThongBaoDatCoc(int roomId,
        string recipientDigest, bool confirm,
        CancellationToken cancellationToken)
    {
        var preview = await notices.PreviewAsync(CurrentActorId, roomId,
            cancellationToken);
        if (!preview.Success || preview.Data is null)
            return NotFound(preview.Message);
        if (!confirm)
            return View(nameof(ThongBaoDatCoc), new RoomNoticePageModel(preview.Data,
                Message: "Cần xác nhận danh sách người nhận trước khi gửi."));
        var result = await notices.SendAsync(CurrentActorId, roomId,
            recipientDigest, cancellationToken);
        return View(nameof(ThongBaoDatCoc), new RoomNoticePageModel(preview.Data,
            result.Data, result.Message));
    }
}
