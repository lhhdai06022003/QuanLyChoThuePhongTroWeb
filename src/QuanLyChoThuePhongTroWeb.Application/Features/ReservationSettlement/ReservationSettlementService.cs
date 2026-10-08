using QuanLyChoThuePhongTroWeb.Application.Abstractions.Security;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;

namespace QuanLyChoThuePhongTroWeb.Application.Features.ReservationSettlement;

public sealed class ReservationSettlementService(IReservationSettlementStore store,
    IEmployeeAccessService employeeAccess, TimeProvider clock)
    : IReservationSettlementService
{
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
        CancellationToken cancellationToken)
    {
        var branchId = await store.GetBranchIdAsync(reservationId, cancellationToken);
        if (!branchId.HasValue)
            return false;
        var scope = await employeeAccess.GetScopeAsync(actorId, cancellationToken);
        return scope is not null && scope.CanAccessBranch(branchId.Value);
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
            SettlementError.Conflict => ServiceResult.Fail("Dữ liệu tiền vừa thay đổi, vui lòng tải lại."),
            _ => ServiceResult.NotFound("Không tìm thấy yêu cầu giữ chỗ.")
        };
}
