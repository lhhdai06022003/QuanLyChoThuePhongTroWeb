namespace QuanLyChoThuePhongTroWeb.Application.Features.Viewings;

public sealed record ViewingAdminRequestDto(int Id, int RoomId, int BranchId,
    string RoomNumber, string GuestName, string Phone, string Email,
    DateTime DesiredTimeUtc, DateTime? ConfirmedTimeUtc, string Status,
    string? Note, string BranchName = "", bool RoomRented = false, string? LastReason = null);

public sealed record ViewingEmailDelivery(bool Sent);

public enum ViewingAdminAction
{
    Confirm,
    Reject,
    MarkViewed,
    CancelBecauseRented
}

public enum ViewingAdminError
{
    None,
    NotFound,
    InvalidState,
    Conflict
}
