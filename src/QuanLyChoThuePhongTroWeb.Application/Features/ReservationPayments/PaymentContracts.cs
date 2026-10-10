using QuanLyChoThuePhongTroWeb.Application.Common.Files;

namespace QuanLyChoThuePhongTroWeb.Application.Features.ReservationPayments;

public sealed record EvidenceSubmission(int PaymentRequestId, UploadFile Image,
    decimal ClaimedAmount, DateTime TransferTimeUtc, string? BankReference,
    string? Note);

// Minh chứng sau khi ảnh đã tải lên Cloudinary; store chỉ ghi URL và PublicId.
public sealed record EvidenceRecord(int PaymentRequestId, string ImageUrl,
    string ImagePublicId, decimal ClaimedAmount, DateTime TransferTimeUtc,
    string? BankReference, string? Note);

public sealed record PaymentEvidenceDto(int Id, int PaymentRequestId,
    decimal ClaimedAmount, DateTime TransferTimeUtc, string Status,
    string? BankReference, string? RejectionReason);

public sealed record PaymentEvidenceAccess(int Id, int GuestUserId, int BranchId,
    string ImageUrl, string? ImagePublicId);

public enum PaymentError
{
    None,
    NotFound,
    InvalidState,
    DuplicateTransaction,
    Conflict
}

public static class ReservationPaymentMarkers
{
    public const string LateRefundRequired = "LATE_REFUND_REQUIRED";
}
