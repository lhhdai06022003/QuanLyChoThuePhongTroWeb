using QuanLyChoThuePhongTroWeb.Application.Features.GiuChos.DTOs;

namespace QuanLyChoThuePhongTroWeb.Application.Features.GiuChos.Persistence;

public interface IHoanTienGiuChoStore
{
    Task<IReadOnlyList<RefundCandidateDto>> GetQueueAsync(int actorId);
    Task<IReadOnlyList<RefundCandidateDto>> GetMineAsync(int actorId);
    Task<bool> DecideAsync(int reservationId, RefundDecisionRequest request, int actorId);
    Task<bool> RecordAsync(int decisionId, RecordRefundRequest request, int actorId);
}
