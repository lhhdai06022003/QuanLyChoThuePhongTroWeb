using QuanLyChoThuePhongTroWeb.Application.Common.Models;

namespace QuanLyChoThuePhongTroWeb.Application.Features.Viewings;

public interface IViewingSlotAdminService
{
    Task<ServiceResult<int>> CreateAsync(int actorId, int roomId,
        DateTime startUtc, DateTime endUtc, int capacity,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ViewingSlotAdminDto>> ListAsync(IReadOnlyCollection<int> roomIds,
        CancellationToken cancellationToken = default);
}
