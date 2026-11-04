namespace QuanLyChoThuePhongTroWeb.Application.Features.Reservations;

public interface IReservationStore
{
    Task ExpireUnpaidAsync(DateTime nowUtc,
        CancellationToken cancellationToken = default);
    Task<ReservationCreateResult> CreateAsync(int actorId, int roomId, int? viewingId,
        DateTime nowUtc, CancellationToken cancellationToken = default);
    // Đọc phong_tro."TienCocPhong" trong lúc khóa dòng phòng và chốt số tiền vào yêu cầu giữ chỗ.
    Task<ReservationCreateResult> CreateForPaymentAsync(int actorId, int roomId, int? viewingId,
        DateTime nowUtc, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ReservationDto>> ListMineAsync(int actorId,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ReservationDto>> ListForStaffAsync(IReadOnlyCollection<int>? branchIds,
        CancellationToken cancellationToken = default);
    Task<int?> GetBranchIdAsync(int reservationId,
        CancellationToken cancellationToken = default);
    Task<ReservationError> ApproveAsync(int actorId, int reservationId, decimal amount,
        DateTime paymentDeadlineUtc, DateTime contractDeadlineUtc, DateTime nowUtc,
        CancellationToken cancellationToken = default);
    Task<ReservationError> CancelAsync(int actorId, int reservationId, DateTime nowUtc,
        CancellationToken cancellationToken = default);
    Task<ReservationError> ExtendContractDeadlineAsync(int actorId,
        int reservationId, DateTime newDeadlineUtc, DateTime nowUtc,
        CancellationToken cancellationToken = default);
    Task<ReservationError> CancelExpiredContractAsync(int actorId,
        int reservationId, string reason, DateTime nowUtc,
        CancellationToken cancellationToken = default);
}
