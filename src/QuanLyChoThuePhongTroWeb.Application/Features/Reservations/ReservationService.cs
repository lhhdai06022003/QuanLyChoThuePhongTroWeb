using QuanLyChoThuePhongTroWeb.Application.Abstractions.Security;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;

namespace QuanLyChoThuePhongTroWeb.Application.Features.Reservations;

public sealed class ReservationService(IReservationStore store,
    IEmployeeAccessService employeeAccess, TimeProvider clock) : IReservationService
{
    public async Task<IReadOnlyList<ReservationDto>> ListMineAsync(int actorId,
        CancellationToken cancellationToken = default)
    {
        await store.ExpireUnpaidAsync(clock.GetUtcNow().UtcDateTime,
            cancellationToken);
        return await store.ListMineAsync(actorId, cancellationToken);
    }

    public async Task<IReadOnlyList<ReservationDto>> ListForStaffAsync(int actorId,
        CancellationToken cancellationToken = default)
    {
        await store.ExpireUnpaidAsync(clock.GetUtcNow().UtcDateTime,
            cancellationToken);
        var scope = await employeeAccess.GetScopeAsync(actorId, cancellationToken);
        return scope is null ? [] : await store.ListForStaffAsync(scope.AllowedBranchIds,
            cancellationToken);
    }

    public async Task<ServiceResult<int>> RequestAsync(int actorId, int roomId, int? viewingId,
        CancellationToken cancellationToken = default)
    {
        if (actorId <= 0 || roomId <= 0 || viewingId is <= 0)
            return ServiceResult<int>.Fail("Thông tin giữ chỗ không hợp lệ.");
        await store.ExpireUnpaidAsync(clock.GetUtcNow().UtcDateTime,
            cancellationToken);
        var result = await store.CreateAsync(actorId, roomId, viewingId,
            clock.GetUtcNow().UtcDateTime, cancellationToken);
        return result.Id.HasValue
            ? ServiceResult<int>.Ok(result.Id.Value, "Đã gửi yêu cầu giữ chỗ.")
            : result.Error switch
            {
                ReservationError.GuestMissing => ServiceResult<int>.Forbidden("Cần đăng nhập tài khoản khách vãng lai."),
                ReservationError.RoomUnavailable => ServiceResult<int>.NotFound("Phòng không còn trống hoặc công khai."),
                ReservationError.ViewingUnavailable => ServiceResult<int>.Fail("Lịch xem không thuộc phòng hoặc tài khoản của bạn."),
                ReservationError.Duplicate => ServiceResult<int>.Fail("Bạn đã có yêu cầu giữ chỗ đang chờ cho phòng này."),
                _ => ServiceResult<int>.Fail("Không thể gửi yêu cầu giữ chỗ lúc này.")
            };
    }

    public async Task<ServiceResult> ApproveAsync(int actorId, int reservationId,
        decimal amount, DateTime paymentDeadlineUtc, DateTime contractDeadlineUtc,
        CancellationToken cancellationToken = default)
    {
        var nowUtc = clock.GetUtcNow().UtcDateTime;
        if (reservationId <= 0 || amount <= 0 || decimal.Round(amount, 2) != amount ||
            paymentDeadlineUtc.Kind != DateTimeKind.Utc ||
            contractDeadlineUtc.Kind != DateTimeKind.Utc ||
            paymentDeadlineUtc <= nowUtc || contractDeadlineUtc <= paymentDeadlineUtc)
            return ServiceResult.Fail("Số tiền hoặc hạn thanh toán, ký hợp đồng không hợp lệ.");

        var branchId = await store.GetBranchIdAsync(reservationId, cancellationToken);
        if (!branchId.HasValue)
            return ServiceResult.NotFound("Không tìm thấy yêu cầu giữ chỗ.");
        var scope = await employeeAccess.GetScopeAsync(actorId, cancellationToken);
        if (scope is null || !scope.CanAccessBranch(branchId.Value))
            return ServiceResult.Forbidden("Bạn không có quyền duyệt phòng này.");

        await store.ExpireUnpaidAsync(nowUtc, cancellationToken);

        var error = await store.ApproveAsync(actorId, reservationId, amount,
            paymentDeadlineUtc, contractDeadlineUtc, nowUtc, cancellationToken);
        return error switch
        {
            ReservationError.None => ServiceResult.Ok("Đã duyệt yêu cầu và tạo thông tin chuyển tiền."),
            ReservationError.Conflict => ServiceResult.Fail("Phòng vừa được giữ bởi yêu cầu khác."),
            ReservationError.InvalidState => ServiceResult.Fail("Yêu cầu đã được xử lý hoặc phòng không còn khả dụng."),
            _ => ServiceResult.NotFound("Không tìm thấy yêu cầu giữ chỗ.")
        };
    }

    public async Task<ServiceResult> CancelAsync(int actorId, int reservationId,
        CancellationToken cancellationToken = default)
    {
        if (actorId <= 0 || reservationId <= 0)
            return ServiceResult.NotFound("Không tìm thấy yêu cầu giữ chỗ.");
        var error = await store.CancelAsync(actorId, reservationId,
            clock.GetUtcNow().UtcDateTime, cancellationToken);
        return error switch
        {
            ReservationError.None => ServiceResult.Ok("Đã hủy yêu cầu. Nếu đã nhận tiền, nhân viên sẽ xử lý hoàn."),
            ReservationError.InvalidState => ServiceResult.Fail("Yêu cầu này không thể hủy."),
            _ => ServiceResult.NotFound("Không tìm thấy yêu cầu giữ chỗ.")
        };
    }

    public async Task<ServiceResult> ExtendContractDeadlineAsync(int actorId,
        int reservationId, DateTime newDeadlineUtc,
        CancellationToken cancellationToken = default)
    {
        var nowUtc = clock.GetUtcNow().UtcDateTime;
        if (reservationId <= 0 || newDeadlineUtc.Kind != DateTimeKind.Utc ||
            newDeadlineUtc <= nowUtc)
            return ServiceResult.Fail("Hạn ký hợp đồng mới phải ở tương lai.");
        var access = await CheckStaffAccessAsync(actorId, reservationId,
            cancellationToken);
        if (!access.Success)
            return access;
        var error = await store.ExtendContractDeadlineAsync(actorId, reservationId,
            newDeadlineUtc, nowUtc, cancellationToken);
        return error switch
        {
            ReservationError.None => ServiceResult.Ok("Đã gia hạn ký hợp đồng."),
            ReservationError.InvalidState => ServiceResult.Fail("Chỉ gia hạn yêu cầu đã nhận tiền và quá hạn ký."),
            ReservationError.Conflict => ServiceResult.Fail("Yêu cầu vừa thay đổi, vui lòng tải lại."),
            _ => ServiceResult.NotFound("Không tìm thấy yêu cầu giữ chỗ.")
        };
    }

    public async Task<ServiceResult> CancelExpiredContractAsync(int actorId,
        int reservationId, string reason,
        CancellationToken cancellationToken = default)
    {
        if (reservationId <= 0 || string.IsNullOrWhiteSpace(reason) ||
            reason.Trim().Length > 500)
            return ServiceResult.Fail("Cần nhập lý do hủy hợp lệ.");
        var access = await CheckStaffAccessAsync(actorId, reservationId,
            cancellationToken);
        if (!access.Success)
            return access;
        var error = await store.CancelExpiredContractAsync(actorId,
            reservationId, reason.Trim(), clock.GetUtcNow().UtcDateTime,
            cancellationToken);
        return error switch
        {
            ReservationError.None => ServiceResult.Ok("Đã hủy giữ chỗ; nhân viên cần quyết định hoàn tiền."),
            ReservationError.InvalidState => ServiceResult.Fail("Chỉ hủy theo hạn ký khi yêu cầu đã nhận tiền và quá hạn."),
            ReservationError.Conflict => ServiceResult.Fail("Yêu cầu vừa thay đổi, vui lòng tải lại."),
            _ => ServiceResult.NotFound("Không tìm thấy yêu cầu giữ chỗ.")
        };
    }

    private async Task<ServiceResult> CheckStaffAccessAsync(int actorId,
        int reservationId, CancellationToken cancellationToken)
    {
        var branchId = await store.GetBranchIdAsync(reservationId,
            cancellationToken);
        if (!branchId.HasValue)
            return ServiceResult.NotFound("Không tìm thấy yêu cầu giữ chỗ.");
        var scope = await employeeAccess.GetScopeAsync(actorId, cancellationToken);
        return scope is not null && scope.CanAccessBranch(branchId.Value)
            ? ServiceResult.Ok()
            : ServiceResult.Forbidden("Bạn không có quyền xử lý phòng này.");
    }
}
