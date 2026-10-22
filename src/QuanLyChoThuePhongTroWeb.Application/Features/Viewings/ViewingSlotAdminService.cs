using QuanLyChoThuePhongTroWeb.Application.Abstractions.Security;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;

namespace QuanLyChoThuePhongTroWeb.Application.Features.Viewings;

public sealed class ViewingSlotAdminService(IViewingSlotAdminStore store,
    IEmployeeAccessService employeeAccess, TimeProvider clock) : IViewingSlotAdminService
{
    public async Task<ServiceResult<int>> CreateAsync(int actorId, int roomId,
        DateTime startUtc, DateTime endUtc, int capacity,
        CancellationToken cancellationToken = default)
    {
        var nowUtc = clock.GetUtcNow().UtcDateTime;
        if (roomId <= 0 || capacity <= 0 || capacity > 100 ||
            startUtc.Kind != DateTimeKind.Utc || endUtc.Kind != DateTimeKind.Utc ||
            startUtc <= nowUtc || endUtc <= startUtc)
            return ServiceResult<int>.Fail("Khung giờ không hợp lệ.");

        var branchId = await store.GetPublishedRoomBranchIdAsync(roomId, cancellationToken);
        if (!branchId.HasValue)
            return ServiceResult<int>.NotFound("Phòng không còn được đăng công khai.");

        var scope = await employeeAccess.GetScopeAsync(actorId, cancellationToken);
        if (scope is null || !scope.CanAccessBranch(branchId.Value))
            return ServiceResult<int>.Forbidden("Bạn không có quyền quản lý phòng này.");

        var id = await store.TryCreateAsync(actorId, roomId, startUtc, endUtc,
            capacity, nowUtc, cancellationToken);
        return id.HasValue
            ? ServiceResult<int>.Ok(id.Value, "Đã tạo khung giờ.")
            : ServiceResult<int>.Fail("Khung giờ trùng hoặc phòng không còn khả dụng.");
    }

    public Task<IReadOnlyList<ViewingSlotAdminDto>> ListAsync(IReadOnlyCollection<int> roomIds,
        CancellationToken cancellationToken = default) =>
        store.ListAsync(roomIds, clock.GetUtcNow().UtcDateTime, cancellationToken);
}
