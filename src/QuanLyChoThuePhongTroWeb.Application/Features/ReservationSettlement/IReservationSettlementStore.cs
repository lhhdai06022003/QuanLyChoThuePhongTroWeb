using QuanLyChoThuePhongTroWeb.Application.Common.Models;

namespace QuanLyChoThuePhongTroWeb.Application.Features.ReservationSettlement;

public interface IReservationSettlementStore
{
    Task<int?> GetBranchIdAsync(int reservationId,
        CancellationToken cancellationToken = default);
    Task<ServiceResult<ReservationContractDraft>> GetContractDraftAsync(int reservationId,
        int authorizedBranchId, CancellationToken cancellationToken = default);
    Task<ServiceResult<int>> CreateContractAsync(int actorId, int reservationId,
        int authorizedBranchId, ReservationContractInput input, DateTime nowUtc,
        CancellationToken cancellationToken = default);
    Task<ServiceResult> CloseWithoutRentAsync(int actorId, int reservationId,
        int authorizedBranchId, string reason, bool requireExpired, DateTime nowUtc,
        CancellationToken cancellationToken = default);
    Task<SettlementSummaryDto?> GetAsync(int reservationId,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SettlementSummaryDto>> ListMineAsync(int actorId,
        CancellationToken cancellationToken = default);
    Task<SettlementError> ApplyAsync(int actorId, int reservationId, int contractId,
        decimal amount, DateTime nowUtc, CancellationToken cancellationToken = default);
    Task<SettlementError> DecideRefundAsync(int actorId, int reservationId,
        decimal amount, string reason, DateTime nowUtc,
        CancellationToken cancellationToken = default);
    Task<SettlementError> RecordRefundAsync(int actorId, int reservationId,
        string bankReference, decimal amount, DateTime nowUtc,
        CancellationToken cancellationToken = default);
}
