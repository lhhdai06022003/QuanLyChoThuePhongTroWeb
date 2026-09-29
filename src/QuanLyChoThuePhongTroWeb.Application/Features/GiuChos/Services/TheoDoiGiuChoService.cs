using QuanLyChoThuePhongTroWeb.Application.Features.GiuChos.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.GiuChos.Persistence;

namespace QuanLyChoThuePhongTroWeb.Application.Features.GiuChos.Services;

public sealed class TheoDoiGiuChoService(ITheoDoiGiuChoStore store) : ITheoDoiGiuChoService
{
    public Task<ReservationProgressDto?> GetMineAsync(int reservationId, int actorId) =>
        reservationId <= 0 || actorId <= 0 ? Task.FromResult<ReservationProgressDto?>(null)
            : store.GetMineAsync(reservationId, actorId);
}
