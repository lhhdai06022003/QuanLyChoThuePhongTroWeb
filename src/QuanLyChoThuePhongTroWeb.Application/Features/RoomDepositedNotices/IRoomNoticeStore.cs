namespace QuanLyChoThuePhongTroWeb.Application.Features.RoomDepositedNotices;

public interface IRoomNoticeStore
{
    Task<RoomNoticePreview?> GetPreviewAsync(int roomId, DateTime nowUtc,
        CancellationToken cancellationToken = default);
}
