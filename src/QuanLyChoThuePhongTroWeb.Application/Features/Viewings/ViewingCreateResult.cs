namespace QuanLyChoThuePhongTroWeb.Application.Features.Viewings;

public enum ViewingCreateError
{
    None,
    GuestMissing,
    RoomUnavailable,
    SlotUnavailable,
    SlotFull,
    Conflict
}

public sealed record ViewingCreateResult(int? RequestId, ViewingCreateError Error);
