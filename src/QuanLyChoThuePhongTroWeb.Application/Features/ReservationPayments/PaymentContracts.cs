namespace QuanLyChoThuePhongTroWeb.Application.Features.ReservationPayments;

public sealed record EvidenceSubmission(int PaymentRequestId, string PrivateFileName,
    decimal ClaimedAmount, DateTime TransferTimeUtc, string? BankReference,
    string? Note);

public sealed record PaymentEvidenceDto(int Id, int PaymentRequestId,
    decimal ClaimedAmount, DateTime TransferTimeUtc, string Status,
    string? BankReference, string? RejectionReason);

public sealed record PaymentEvidenceAccess(int Id, int GuestUserId, int BranchId,
    string PrivateFileName);

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
