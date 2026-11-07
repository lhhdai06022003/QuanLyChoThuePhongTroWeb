using QuanLyChoThuePhongTroWeb.Application.Common.Models;

namespace QuanLyChoThuePhongTroWeb.Application.Features.ReservationSettlement;

public interface IReservationSettlementService
{
    Task<ServiceResult<ReservationContractDraft>> GetContractDraftAsync(int actorId,
        int reservationId, CancellationToken cancellationToken = default);
    Task<ServiceResult<int>> CreateContractAsync(int actorId, int reservationId,
        ReservationContractInput input, CancellationToken cancellationToken = default);
    Task<ServiceResult> CloseWithoutRentAsync(int actorId, int reservationId, string reason,
        bool confirm, bool preservePriorRefund,
        CancellationToken cancellationToken = default);
    Task<ServiceResult> CancelExpiredContractAsync(int actorId, int reservationId, string reason,
        bool confirm, bool preservePriorRefund,
        CancellationToken cancellationToken = default);
    Task<ServiceResult> ApplyAsync(int actorId, int reservationId, int contractId,
        decimal amount, CancellationToken cancellationToken = default);
    Task<ServiceResult> DecideRefundAsync(int actorId, int reservationId,
        decimal amount, string reason, CancellationToken cancellationToken = default);
    Task<ServiceResult> RecordRefundAsync(int actorId, int reservationId,
        string bankReference, decimal amount, CancellationToken cancellationToken = default);
    Task<SettlementSummaryDto?> GetForStaffAsync(int actorId, int reservationId,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SettlementSummaryDto>> ListMineAsync(int actorId,
        CancellationToken cancellationToken = default);
}
