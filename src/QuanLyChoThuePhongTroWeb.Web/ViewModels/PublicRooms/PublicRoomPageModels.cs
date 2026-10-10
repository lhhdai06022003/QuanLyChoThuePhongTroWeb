using QuanLyChoThuePhongTroWeb.Application.Features.PublicRooms;

namespace QuanLyChoThuePhongTroWeb.ViewModels.PublicRooms;

public sealed record PublicRoomCardModel(PublicRoomDto Room, IReadOnlyList<string> Images);

public sealed record PublicRoomListModel(
    IReadOnlyList<PublicRoomCardModel> Items,
    int TotalCount,
    int Page,
    int PageSize,
    decimal? GiaTu,
    decimal? GiaDen,
    double? DienTichTu,
    string? KhuVuc);
