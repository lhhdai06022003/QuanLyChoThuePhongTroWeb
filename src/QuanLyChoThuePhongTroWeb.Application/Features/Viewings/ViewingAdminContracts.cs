namespace QuanLyChoThuePhongTroWeb.Application.Features.Viewings;

public sealed record ViewingAdminRequestDto(int Id, int RoomId, int BranchId,
    string RoomNumber, string GuestName, string Phone, string Email,
    DateTime DesiredTimeUtc, DateTime? ConfirmedTimeUtc, string Status,
    string? Note);

public enum ViewingAdminAction
{
    Confirm,
    Reject,
    MarkViewed
}

public enum ViewingAdminError
{
    None,
    NotFound,
    InvalidState,
    Conflict
}
