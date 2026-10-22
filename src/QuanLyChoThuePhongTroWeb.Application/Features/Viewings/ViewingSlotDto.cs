namespace QuanLyChoThuePhongTroWeb.Application.Features.Viewings;

public sealed record ViewingSlotDto(int Id, DateTime StartUtc, DateTime EndUtc,
    int RemainingPlaces);
