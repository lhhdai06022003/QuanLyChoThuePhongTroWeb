using QuanLyChoThuePhongTroWeb.Application.Common.Models;

namespace QuanLyChoThuePhongTroWeb.Application.Features.Viewings;

public interface IViewingRequestService
{
    Task<IReadOnlyList<ViewingSlotDto>> ListAvailableSlotsAsync(int roomId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ViewingRequestDto>> ListMineAsync(int actorId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult> CancelAsync(int actorId, int requestId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<int>> SubmitAsync(int actorId, ViewingRequestInput input,
        CancellationToken cancellationToken = default);
}
