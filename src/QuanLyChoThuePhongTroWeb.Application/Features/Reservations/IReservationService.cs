using QuanLyChoThuePhongTroWeb.Application.Common.Models;

namespace QuanLyChoThuePhongTroWeb.Application.Features.Reservations;

public interface IReservationService
{
    Task<ServiceResult<int>> RequestAsync(int actorId, int roomId, int? viewingId,
        CancellationToken cancellationToken = default);
    Task<ServiceResult> ApproveAsync(int actorId, int reservationId, decimal amount,
        DateTime paymentDeadlineUtc, DateTime contractDeadlineUtc,
        CancellationToken cancellationToken = default);
    Task<ServiceResult> CancelAsync(int actorId, int reservationId,
        CancellationToken cancellationToken = default);
    Task<ServiceResult> ExtendContractDeadlineAsync(int actorId,
        int reservationId, DateTime newDeadlineUtc,
        CancellationToken cancellationToken = default);
    Task<ServiceResult> CancelExpiredContractAsync(int actorId,
        int reservationId, string reason,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ReservationDto>> ListMineAsync(int actorId,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ReservationDto>> ListForStaffAsync(int actorId,
        CancellationToken cancellationToken = default);
}
