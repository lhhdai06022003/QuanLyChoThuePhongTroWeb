namespace QuanLyChoThuePhongTroWeb.Application.Features.Viewings;

public sealed record ViewingRequestDto(int Id, int RoomId, string RoomNumber,
    DateTime DesiredTimeUtc, DateTime? ConfirmedTimeUtc, string Status,
    bool CanCancel);

public enum ViewingCancelError
{
    None,
    NotFound,
    TooLateOrClosed
}
