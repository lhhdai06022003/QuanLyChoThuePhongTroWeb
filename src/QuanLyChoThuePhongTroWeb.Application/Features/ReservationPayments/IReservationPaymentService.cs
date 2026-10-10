using QuanLyChoThuePhongTroWeb.Application.Abstractions.Services;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;

namespace QuanLyChoThuePhongTroWeb.Application.Features.ReservationPayments;

public interface IReservationPaymentService
{
    Task<ServiceResult<int>> SubmitEvidenceAsync(int actorId, EvidenceSubmission submission,
        CancellationToken cancellationToken = default);
    Task<ServiceResult> ConfirmReceivedAsync(int actorId, int paymentRequestId,
        int? evidenceId, string bankReference, decimal actualAmount,
        DateTime receivedAtUtc,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PaymentEvidenceDto>> ListMineAsync(int actorId,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PaymentEvidenceDto>> ListForReservationAsync(int actorId,
        int reservationId, int branchId, CancellationToken cancellationToken = default);
    Task<ServiceResult<MeterImageReadResult>> GetEvidenceImageAsync(int actorId,
        int evidenceId, CancellationToken cancellationToken = default);
}
