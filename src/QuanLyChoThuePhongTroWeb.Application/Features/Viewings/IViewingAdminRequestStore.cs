namespace QuanLyChoThuePhongTroWeb.Application.Features.Viewings;

public interface IViewingAdminRequestStore
{
    Task<IReadOnlyList<ViewingAdminRequestDto>> ListAsync(
        IReadOnlyCollection<int>? branchIds,
        CancellationToken cancellationToken = default);
    Task<int?> GetBranchIdAsync(int requestId,
        CancellationToken cancellationToken = default);
    Task<ViewingAdminError> UpdateAsync(int actorId, int requestId,
        ViewingAdminAction action, DateTime? confirmedTimeUtc, string? reason,
        DateTime nowUtc, CancellationToken cancellationToken = default);
}
