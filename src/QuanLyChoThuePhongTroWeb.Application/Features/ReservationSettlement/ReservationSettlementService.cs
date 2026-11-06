using QuanLyChoThuePhongTroWeb.Application.Abstractions.Security;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;

namespace QuanLyChoThuePhongTroWeb.Application.Features.ReservationSettlement;

public sealed class ReservationSettlementService(IReservationSettlementStore store,
    IEmployeeAccessService employeeAccess, TimeProvider clock)
    : IReservationSettlementService
{
    private const string Unauthorized = "Bạn không có quyền xử lý yêu cầu này.";

    public async Task<ServiceResult<ReservationContractDraft>> GetContractDraftAsync(int actorId,
        int reservationId, CancellationToken cancellationToken = default)
    {
        var branchId = await GetAuthorizedBranchIdAsync(actorId, reservationId, cancellationToken);
        return branchId.HasValue
            ? await store.GetContractDraftAsync(reservationId, branchId.Value, cancellationToken)
            : ServiceResult<ReservationContractDraft>.Forbidden(Unauthorized);
    }

    public async Task<ServiceResult<int>> CreateContractAsync(int actorId, int reservationId,
        ReservationContractInput input, CancellationToken cancellationToken = default)
    {
        var branchId = await GetAuthorizedBranchIdAsync(actorId, reservationId, cancellationToken);
        if (!branchId.HasValue)
            return ServiceResult<int>.Forbidden(Unauthorized);
        if (input.StartDate is null || input.StartDate == DateOnly.MinValue)
            return ServiceResult<int>.Fail("Vui lòng nhập ngày bắt đầu thuê hợp lệ.");
        if (input.EndDate < input.StartDate)
            return ServiceResult<int>.Fail("Ngày kết thúc không được trước ngày bắt đầu.");
        if (input.Rent < 1m || !ValidMoney(input.Rent, false) || input.Rent > 9_999_999_999_999_999.99m)
            return ServiceResult<int>.Fail("Tiền thuê phòng không hợp lệ.");

        return await store.CreateContractAsync(actorId, reservationId, branchId.Value, input,
            clock.GetUtcNow().UtcDateTime, cancellationToken);
    }

    public Task<ServiceResult> CloseWithoutRentAsync(int actorId, int reservationId, string reason,
        CancellationToken cancellationToken = default) =>
        CloseAsync(actorId, reservationId, reason, requireExpired: false, cancellationToken);

    public Task<ServiceResult> CancelExpiredContractAsync(int actorId, int reservationId, string reason,
        CancellationToken cancellationToken = default) =>
        CloseAsync(actorId, reservationId, reason, requireExpired: true, cancellationToken);

    private async Task<ServiceResult> CloseAsync(int actorId, int reservationId, string reason,
        bool requireExpired, CancellationToken cancellationToken)
    {
        var trimmedReason = reason?.Trim();
        if (reservationId <= 0 || string.IsNullOrEmpty(trimmedReason) || trimmedReason.Length > 500)
            return ServiceResult.Fail("Cần nhập lý do hợp lệ để kết thúc giữ chỗ.");
        var branchId = await GetAuthorizedBranchIdAsync(actorId, reservationId, cancellationToken);
        if (!branchId.HasValue)
            return ServiceResult.Forbidden(Unauthorized);

        return await store.CloseWithoutRentAsync(actorId, reservationId, branchId.Value, trimmedReason,
            requireExpired, clock.GetUtcNow().UtcDateTime, cancellationToken);
    }

    public Task<IReadOnlyList<SettlementSummaryDto>> ListMineAsync(int actorId,
        CancellationToken cancellationToken = default) =>
        store.ListMineAsync(actorId, cancellationToken);

    public async Task<SettlementSummaryDto?> GetForStaffAsync(int actorId,
        int reservationId, CancellationToken cancellationToken = default) =>
        await CanAccessAsync(actorId, reservationId, cancellationToken)
            ? await store.GetAsync(reservationId, cancellationToken) : null;

    public async Task<ServiceResult> ApplyAsync(int actorId, int reservationId,
        int contractId, decimal amount, CancellationToken cancellationToken = default)
    {
        if (reservationId <= 0 || contractId <= 0 || !ValidMoney(amount, false))
            return ServiceResult.Fail("Hợp đồng hoặc số tiền áp dụng không hợp lệ.");
        if (!await CanAccessAsync(actorId, reservationId, cancellationToken))
            return ServiceResult.Forbidden("Bạn không có quyền xử lý yêu cầu này.");
        return Map(await store.ApplyAsync(actorId, reservationId, contractId,
            amount, clock.GetUtcNow().UtcDateTime, cancellationToken),
            "Đã áp dụng tiền giữ chỗ vào tiền cọc hợp đồng.");
    }

    public async Task<ServiceResult> DecideRefundAsync(int actorId,
        int reservationId, decimal amount, string reason,
        CancellationToken cancellationToken = default)
    {
        if (reservationId <= 0 || !ValidMoney(amount, true) ||
            string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 500)
            return ServiceResult.Fail("Số tiền hoặc lý do hoàn không hợp lệ.");
        if (!await CanAccessAsync(actorId, reservationId, cancellationToken))
            return ServiceResult.Forbidden("Bạn không có quyền xử lý yêu cầu này.");
        return Map(await store.DecideRefundAsync(actorId, reservationId,
            amount, reason.Trim(), clock.GetUtcNow().UtcDateTime,
            cancellationToken), "Đã lưu quyết định hoàn tiền.");
    }

    public async Task<ServiceResult> RecordRefundAsync(int actorId,
        int reservationId, string bankReference, decimal amount,
        CancellationToken cancellationToken = default)
    {
        if (reservationId <= 0 || !ValidMoney(amount, false) ||
            string.IsNullOrWhiteSpace(bankReference) ||
            bankReference.Trim().Length > 100)
            return ServiceResult.Fail("Mã giao dịch hoàn hoặc số tiền không hợp lệ.");
        if (!await CanAccessAsync(actorId, reservationId, cancellationToken))
            return ServiceResult.Forbidden("Bạn không có quyền xử lý yêu cầu này.");
        return Map(await store.RecordRefundAsync(actorId, reservationId,
            bankReference.Trim(), amount, clock.GetUtcNow().UtcDateTime,
            cancellationToken), "Đã ghi nhận giao dịch hoàn thực tế.");
    }

    private async Task<bool> CanAccessAsync(int actorId, int reservationId,
        CancellationToken cancellationToken) =>
        (await GetAuthorizedBranchIdAsync(actorId, reservationId, cancellationToken)).HasValue;

    private async Task<int?> GetAuthorizedBranchIdAsync(int actorId, int reservationId,
        CancellationToken cancellationToken)
    {
        var branchId = await store.GetBranchIdAsync(reservationId, cancellationToken);
        if (!branchId.HasValue)
            return null;
        var scope = await employeeAccess.GetScopeAsync(actorId, cancellationToken);
        return scope is not null && scope.CanAccessBranch(branchId.Value) ? branchId : null;
    }

    private static bool ValidMoney(decimal amount, bool allowZero) =>
        (allowZero ? amount >= 0 : amount > 0) &&
        decimal.Round(amount, 2) == amount;

    private static ServiceResult Map(SettlementError error, string successMessage) =>
        error switch
        {
            SettlementError.None => ServiceResult.Ok(successMessage),
            SettlementError.ContractMismatch => ServiceResult.Fail("Hợp đồng không cùng phòng hoặc không khớp thông tin khách."),
            SettlementError.AmountExceedsAvailable => ServiceResult.Fail("Số tiền vượt phần đã nhận còn có thể xử lý."),
            SettlementError.BelowMandatoryRefund => ServiceResult.Fail("Số tiền hoàn thấp hơn phần bắt buộc hoàn do chuyển dư hoặc đến muộn."),
            SettlementError.DuplicateTransaction => ServiceResult.Fail("Mã giao dịch hoàn đã được ghi nhận."),
            SettlementError.InvalidState => ServiceResult.Fail("Trạng thái yêu cầu không cho phép thao tác này."),
            SettlementError.ContractConfirmationRequired => ServiceResult.Fail("Vui lòng dùng Xác nhận thuê để tạo hợp đồng và liên kết tài khoản đặt chỗ."),
            SettlementError.Conflict => ServiceResult.Fail("Dữ liệu tiền vừa thay đổi, vui lòng tải lại."),
            _ => ServiceResult.NotFound("Không tìm thấy yêu cầu giữ chỗ.")
        };
}
