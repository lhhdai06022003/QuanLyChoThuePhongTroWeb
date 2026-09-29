using QuanLyChoThuePhongTroWeb.Application.Features.GiuChos.DTOs;

namespace QuanLyChoThuePhongTroWeb.Application.Features.GiuChos.Persistence;

public interface IGiuChoStore
{
    Task<int?> TryCreateAsync(string code, int? viewingId, int actorId);
    Task<bool> TryApproveAsync(int id, DateTime deadlineUtc,
        DateTime contractDeadlineUtc, int actorId);
    Task<IReadOnlyList<ReservationDto>> GetMineAsync(int actorId);
    Task<IReadOnlyList<ReservationDto>> GetQueueAsync(int actorId);
    Task<bool> TryCloseUnpaidAsync(int id, int actorId, string reason);
}
