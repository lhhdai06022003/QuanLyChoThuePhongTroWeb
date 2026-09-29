using QuanLyChoThuePhongTroWeb.Application.Features.GiuChos.DTOs;

namespace QuanLyChoThuePhongTroWeb.Application.Features.GiuChos.Persistence;

public interface IThanhToanGiuChoStore
{
    Task<HoldPaymentDto?> GetForGuestAsync(int reservationId, int actorId);
    Task<bool> CanSubmitAsync(int paymentId, int actorId, DateTime nowUtc);
    Task<bool> TrySubmitAsync(int paymentId, int actorId, string key,
        SubmitProofRequest request, DateTime nowUtc);
    Task<IReadOnlyList<ProofReviewDto>> GetReviewQueueAsync(int actorId);
    Task<string?> GetAuthorizedProofKeyAsync(int proofId, int actorId);
    Task<bool> ConfirmAsync(int proofId, int actorId, string bankReference,
        decimal actualAmount, DateTime receivedUtc);
    Task<bool> RejectAsync(int proofId, int actorId, string reason,
        decimal actualAmount, string? bankReference, DateTime? receivedUtc);
}
