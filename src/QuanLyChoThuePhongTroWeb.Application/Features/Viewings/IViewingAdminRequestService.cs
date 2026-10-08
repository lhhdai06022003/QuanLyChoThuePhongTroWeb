using QuanLyChoThuePhongTroWeb.Application.Common.Models;

namespace QuanLyChoThuePhongTroWeb.Application.Features.Viewings;

public interface IViewingAdminRequestService
{
    Task<IReadOnlyList<ViewingAdminRequestDto>> ListAsync(int actorId,
        CancellationToken cancellationToken = default);
    Task<ServiceResult> UpdateAsync(int actorId, int requestId,
        ViewingAdminAction action, DateTime? confirmedTimeUtc, string? reason,
        CancellationToken cancellationToken = default);
}
