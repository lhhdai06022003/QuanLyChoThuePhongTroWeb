namespace QuanLyChoThuePhongTroWeb.Application.Features.Viewings;

public interface IViewingSlotAdminStore
{
    Task<int?> GetPublishedRoomBranchIdAsync(int roomId,
        CancellationToken cancellationToken = default);
    Task<int?> TryCreateAsync(int actorId, int roomId, DateTime startUtc,
        DateTime endUtc, int capacity, DateTime nowUtc,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ViewingSlotAdminDto>> ListAsync(IReadOnlyCollection<int> roomIds,
        DateTime nowUtc, CancellationToken cancellationToken = default);
}
