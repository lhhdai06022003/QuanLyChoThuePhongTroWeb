using QuanLyChoThuePhongTroWeb.Application.Features.GiuChos.DTOs;

namespace QuanLyChoThuePhongTroWeb.Application.Features.GiuChos.Services;

public interface ITheoDoiGiuChoService
{
    Task<ReservationProgressDto?> GetMineAsync(int reservationId, int actorId);
}
