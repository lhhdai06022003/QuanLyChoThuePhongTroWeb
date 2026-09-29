using QuanLyChoThuePhongTroWeb.Application.Features.GiuChos.DTOs;

namespace QuanLyChoThuePhongTroWeb.Application.Features.GiuChos.Services;

public interface IGiuChoService
{
    Task<ReservationResult> CreateAsync(CreateReservationRequest request, int actorId);
    Task<ReservationResult> ApproveAsync(int id, ApproveReservationRequest request, int actorId);
    Task<IReadOnlyList<ReservationDto>> GetMineAsync(int actorId);
    Task<IReadOnlyList<ReservationDto>> GetQueueAsync(int actorId);
    Task<ReservationResult> CloseUnpaidAsync(int id, int actorId, string reason);
}
