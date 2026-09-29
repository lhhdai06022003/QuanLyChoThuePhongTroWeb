using QuanLyChoThuePhongTroWeb.Application.Features.GiuChos.DTOs;

namespace QuanLyChoThuePhongTroWeb.Application.Features.GiuChos.UseCases;

public interface IChuyenHopDongTuGiuChoUseCase
{
    Task<ContractCandidateDto?> GetCandidateAsync(int reservationId, int actorId);
    Task<ReservationContractResult> ExecuteAsync(int reservationId,
        ReservationContractRequest request, int actorId);
}
