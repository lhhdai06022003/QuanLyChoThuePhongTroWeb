namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels;

public sealed record ListingPreviewItem(
    int RoomId,
    string RoomNumber,
    string BranchName,
    decimal MonthlyRent,
    double Area,
    int RoomStatus,
    bool IsVisible,
    int ImageCount,
    string? CoverUrl);

public sealed record ListingPreviewPage(IReadOnlyList<ListingPreviewItem> Rooms)
{
    public int VisibleCount => Rooms.Count(room => room.IsVisible);
    public int ReadyCount => Rooms.Count(room => room.RoomStatus == 0);
}
