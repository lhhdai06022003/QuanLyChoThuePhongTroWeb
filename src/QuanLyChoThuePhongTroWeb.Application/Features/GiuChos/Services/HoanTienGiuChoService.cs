using QuanLyChoThuePhongTroWeb.Application.Features.GiuChos.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.GiuChos.Persistence;

namespace QuanLyChoThuePhongTroWeb.Application.Features.GiuChos.Services;

public sealed class HoanTienGiuChoService(IHoanTienGiuChoStore store) : IHoanTienGiuChoService
{
    public Task<IReadOnlyList<RefundCandidateDto>> GetQueueAsync(int actorId) => store.GetQueueAsync(actorId);
    public Task<IReadOnlyList<RefundCandidateDto>> GetMineAsync(int actorId) => store.GetMineAsync(actorId);

    public async Task<RefundActionResult> DecideAsync(int reservationId,
        RefundDecisionRequest request, int actorId)
    {
        if (reservationId <= 0 || actorId <= 0 || request.SoTienHoanDuyet < 0 ||
            decimal.Round(request.SoTienHoanDuyet, 0) != request.SoTienHoanDuyet ||
            string.IsNullOrWhiteSpace(request.LyDo) || request.LyDo.Length > 500)
            return new(false, "Nhập số tiền hoàn theo đồng và lý do tối đa 500 ký tự.");
        var saved = await store.DecideAsync(reservationId,
            request with { LyDo = request.LyDo.Trim() }, actorId);
        return saved ? new(true, "Đã lưu quyết định hoàn tiền.")
            : new(false, "Không thể duyệt hoàn: kiểm tra quyền, tiền thực nhận hoặc trạng thái giữ chỗ.");
    }

    public async Task<RefundActionResult> RecordAsync(int decisionId,
        RecordRefundRequest request, int actorId)
    {
        if (decisionId <= 0 || actorId <= 0 || request.SoTienHoan <= 0 ||
            decimal.Round(request.SoTienHoan, 0) != request.SoTienHoan ||
            string.IsNullOrWhiteSpace(request.MaGiaoDichHoan) ||
            request.MaGiaoDichHoan.Length > 100 || request.GhiChu?.Length > 500 ||
            request.NgayHoan > DateTimeOffset.UtcNow.AddMinutes(5))
            return new(false, "Thông tin giao dịch hoàn không hợp lệ.");
        var saved = await store.RecordAsync(decisionId, request with
        {
            MaGiaoDichHoan = request.MaGiaoDichHoan.Trim(),
            GhiChu = request.GhiChu?.Trim()
        }, actorId);
        return saved ? new(true, "Đã ghi nhận khoản tiền đã hoàn thực tế.")
            : new(false, "Không thể ghi nhận: kiểm tra mã giao dịch và số dư được duyệt.");
    }
}
