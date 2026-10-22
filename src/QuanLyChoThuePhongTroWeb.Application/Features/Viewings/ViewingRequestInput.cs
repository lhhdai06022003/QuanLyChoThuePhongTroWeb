namespace QuanLyChoThuePhongTroWeb.Application.Features.Viewings;

public sealed record ViewingRequestInput(int RoomId, int? SlotId, DateTime? PreferredTimeUtc,
    string FullName, string Phone, string Email, string? Note);
