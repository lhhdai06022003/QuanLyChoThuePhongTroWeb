namespace QuanLyChoThuePhongTroWeb.Application.Features.Viewings;

public interface IViewingRequestStore
{
    Task<IReadOnlyList<ViewingSlotDto>> ListAvailableSlotsAsync(int roomId,
        DateTime nowUtc, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ViewingRequestDto>> ListMineAsync(int actorId, DateTime nowUtc,
        CancellationToken cancellationToken = default);

    Task<ViewingCancelError> CancelAsync(int actorId, int requestId, DateTime nowUtc,
        CancellationToken cancellationToken = default);

    Task<ViewingCreateResult> CreateAsync(int actorId, ViewingRequestInput input,
        DateTime nowUtc, CancellationToken cancellationToken = default);
}
