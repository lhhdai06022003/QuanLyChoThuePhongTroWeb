using QuanLyChoThuePhongTroWeb.Application.Features.PublicRooms;

namespace QuanLyChoThuePhongTroWeb.ViewModels.PublicRooms;

public sealed record PublicRoomCardModel(PublicRoomDto Room, IReadOnlyList<string> Images);
public sealed record PublicRoomLoginPromptModel(PublicRoomCardModel Room, string ActionName, string ReturnUrl);

public sealed record PublicRoomListModel(
    IReadOnlyList<PublicRoomCardModel> Items,
    int TotalCount,
    int Page,
    int PageSize,
    double? GiaTu,
    double? GiaDen,
    double? DienTichTu,
    string? KhuVuc,
    int? SoNguoi = null,
    string SapXep = "mac-dinh",
    IReadOnlyList<PublicRoomAreaOption>? Areas = null,
    PublicRoomCardModel? HeroRoom = null);

public sealed record PublicRoomAreaOption(string Name, int Count, double MinPrice);
public sealed record PublicRoomComparisonModel(IReadOnlyList<PublicRoomCardModel> Rooms, int UnavailableCount);
