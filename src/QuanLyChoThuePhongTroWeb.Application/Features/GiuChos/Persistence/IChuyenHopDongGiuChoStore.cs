using QuanLyChoThuePhongTroWeb.Application.Features.GiuChos.DTOs;

namespace QuanLyChoThuePhongTroWeb.Application.Features.GiuChos.Persistence;

public interface IChuyenHopDongGiuChoStore
{
    Task<ContractCandidateDto?> GetCandidateAsync(int reservationId, int actorId);
    Task<int?> ConvertAsync(int reservationId, ReservationContractRequest request, int actorId);
}
