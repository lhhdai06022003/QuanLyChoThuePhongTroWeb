namespace QuanLyChoThuePhongTroWeb.Application.Features.ReservationPayments;

public interface IReservationPaymentStore
{
    Task<int?> AddEvidenceAsync(int actorId, EvidenceRecord evidence,
        DateTime nowUtc, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PaymentEvidenceDto>> ListMineAsync(int actorId,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PaymentEvidenceDto>> ListForReservationAsync(int reservationId,
        CancellationToken cancellationToken = default);
    Task<PaymentEvidenceAccess?> GetEvidenceAccessAsync(int evidenceId,
        CancellationToken cancellationToken = default);
    Task<int?> GetPaymentBranchIdAsync(int paymentRequestId,
        CancellationToken cancellationToken = default);
    Task<PaymentError> ConfirmReceivedAsync(int actorId, int paymentRequestId,
        int? evidenceId, string bankReference, decimal actualAmount,
        DateTime receivedAtUtc, DateTime nowUtc,
        CancellationToken cancellationToken = default);
}
