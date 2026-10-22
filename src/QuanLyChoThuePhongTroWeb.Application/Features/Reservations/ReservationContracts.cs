namespace QuanLyChoThuePhongTroWeb.Application.Features.Reservations;

public sealed record ReservationDto(int Id, int RoomId, string RoomNumber, int BranchId,
    string GuestName, string Status, decimal? HoldAmount, DateTime? PaymentDeadlineUtc,
    DateTime? ContractDeadlineUtc, int? PaymentRequestId, string? PaymentCode,
    string? PaymentStatus,
    decimal ConfirmedAmount, bool CanCancel);

public enum ReservationError
{
    None,
    NotFound,
    GuestMissing,
    RoomUnavailable,
    ViewingUnavailable,
    Duplicate,
    InvalidState,
    Conflict
}

public sealed record ReservationCreateResult(int? Id, ReservationError Error);
