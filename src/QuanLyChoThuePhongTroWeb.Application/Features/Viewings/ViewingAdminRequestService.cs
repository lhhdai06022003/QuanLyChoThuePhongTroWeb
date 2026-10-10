using QuanLyChoThuePhongTroWeb.Application.Abstractions.Security;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;

namespace QuanLyChoThuePhongTroWeb.Application.Features.Viewings;

public sealed class ViewingAdminRequestService(IViewingAdminRequestStore store,
    IEmployeeAccessService employeeAccess, TimeProvider clock)
    : IViewingAdminRequestService
{
    public async Task<IReadOnlyList<ViewingAdminRequestDto>> ListAsync(int actorId,
        CancellationToken cancellationToken = default)
    {
        var scope = await employeeAccess.GetScopeAsync(actorId, cancellationToken);
        return scope is null ? [] : await store.ListAsync(scope.AllowedBranchIds,
            cancellationToken);
    }

    public async Task<ServiceResult> UpdateAsync(int actorId, int requestId,
        ViewingAdminAction action, DateTime? confirmedTimeUtc, string? reason,
        CancellationToken cancellationToken = default)
    {
        var nowUtc = clock.GetUtcNow().UtcDateTime;
        if (actorId <= 0 || requestId <= 0 ||
            action == ViewingAdminAction.Confirm &&
                (confirmedTimeUtc is null || confirmedTimeUtc.Value.Kind != DateTimeKind.Utc ||
                 confirmedTimeUtc.Value <= nowUtc) ||
            reason?.Length > 500 ||
            action == ViewingAdminAction.Reject && string.IsNullOrWhiteSpace(reason))
            return ServiceResult.Fail("Thông tin xử lý lịch xem không hợp lệ.");
        var branchId = await store.GetBranchIdAsync(requestId, cancellationToken);
        if (!branchId.HasValue)
            return ServiceResult.NotFound("Không tìm thấy lịch xem.");
        var scope = await employeeAccess.GetScopeAsync(actorId, cancellationToken);
        if (scope is null || !scope.CanAccessBranch(branchId.Value))
            return ServiceResult.Forbidden("Bạn không có quyền xử lý lịch này.");
        var error = await store.UpdateAsync(actorId, requestId, action,
            confirmedTimeUtc, reason?.Trim(), nowUtc, cancellationToken);
        return error switch
        {
            ViewingAdminError.None => ServiceResult.Ok("Đã cập nhật lịch xem."),
            ViewingAdminError.InvalidState => ServiceResult.Fail("Lịch xem đã thay đổi hoặc không còn xử lý được."),
            ViewingAdminError.Conflict => ServiceResult.Fail("Lịch xem vừa thay đổi, vui lòng tải lại."),
            _ => ServiceResult.NotFound("Không tìm thấy lịch xem.")
        };
    }
}
