using QuanLyChoThuePhongTroWeb.Application.Features.GiuChos.DTOs;

namespace QuanLyChoThuePhongTroWeb.Application.Features.GiuChos.Persistence;

public interface ITheoDoiGiuChoStore
{
    Task<ReservationProgressDto?> GetMineAsync(int reservationId, int actorId);
}
