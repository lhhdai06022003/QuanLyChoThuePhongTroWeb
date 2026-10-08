namespace QuanLyChoThuePhongTroWeb.Application.Features.ReservationSettlement;

public interface IReservationSettlementStore
{
    Task<int?> GetBranchIdAsync(int reservationId,
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
