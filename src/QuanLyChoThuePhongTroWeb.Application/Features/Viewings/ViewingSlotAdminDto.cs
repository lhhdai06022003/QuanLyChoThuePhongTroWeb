namespace QuanLyChoThuePhongTroWeb.Application.Features.Viewings;

public sealed record ViewingSlotAdminDto(int Id, int RoomId, DateTime StartUtc,
    DateTime EndUtc, int Capacity, int Booked);
