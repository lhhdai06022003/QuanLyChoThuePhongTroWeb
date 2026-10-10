using QuanLyChoThuePhongTroWeb.Application.Common.Models;

namespace QuanLyChoThuePhongTroWeb.Application.Features.RoomDepositedNotices;

public interface IRoomNoticeService
{
    Task<ServiceResult<RoomNoticePreview>> PreviewAsync(int actorId, int roomId,
        CancellationToken cancellationToken = default);
    Task<ServiceResult<IReadOnlyList<RoomNoticeDelivery>>> SendAsync(int actorId,
        int roomId, string recipientDigest,
        CancellationToken cancellationToken = default);
}
