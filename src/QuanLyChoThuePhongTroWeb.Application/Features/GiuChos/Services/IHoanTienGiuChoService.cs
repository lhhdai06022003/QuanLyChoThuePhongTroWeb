using QuanLyChoThuePhongTroWeb.Application.Features.GiuChos.DTOs;

namespace QuanLyChoThuePhongTroWeb.Application.Features.GiuChos.Services;

public interface IHoanTienGiuChoService
{
    Task<IReadOnlyList<RefundCandidateDto>> GetQueueAsync(int actorId);
    Task<IReadOnlyList<RefundCandidateDto>> GetMineAsync(int actorId);
    Task<RefundActionResult> DecideAsync(int reservationId, RefundDecisionRequest request, int actorId);
    Task<RefundActionResult> RecordAsync(int decisionId, RecordRefundRequest request, int actorId);
}
