using QuanLyChoThuePhongTroWeb.Application.Common.Files;
using QuanLyChoThuePhongTroWeb.Application.Features.GiuChos.DTOs;

namespace QuanLyChoThuePhongTroWeb.Application.Features.GiuChos.Services;

public interface IThanhToanGiuChoService
{
    Task<HoldPaymentDto?> GetForGuestAsync(int reservationId, int actorId);
    Task<PaymentActionResult> SubmitProofAsync(int paymentId, int actorId,
        SubmitProofRequest request, UploadFile image);
    Task<StoredReservationProof?> ReadProofAsync(int proofId, int actorId);
    Task<IReadOnlyList<ProofReviewDto>> GetReviewQueueAsync(int actorId);
    Task<PaymentActionResult> ConfirmAsync(int proofId, int actorId,
        string bankReference, decimal actualAmount, DateTimeOffset receivedAt);
    Task<PaymentActionResult> RejectAsync(int proofId, int actorId, string reason,
        decimal actualAmount, string? bankReference, DateTimeOffset? receivedAt);
}
