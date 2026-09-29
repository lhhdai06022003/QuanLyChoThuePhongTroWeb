using QuanLyChoThuePhongTroWeb.Application.Features.GiuChos.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.GiuChos.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Features.PhongCongKhais.Services;

namespace QuanLyChoThuePhongTroWeb.Application.Features.GiuChos.Services;

public sealed class GiuChoService(IPhongCongKhaiService rooms, IGiuChoStore store) : IGiuChoService
{
    public async Task<ReservationResult> CreateAsync(CreateReservationRequest request, int actorId)
    {
        if (actorId <= 0) return new(false, null, "Vui lòng đăng nhập để giữ chỗ.");
        if (await rooms.GetAsync(request.MaCongKhai) is null)
            return new(false, null, "Phòng hiện không nhận yêu cầu giữ chỗ.");
        var id = await store.TryCreateAsync(request.MaCongKhai, request.YeuCauXemPhongId, actorId);
        return id is int holdId
            ? new(true, holdId, "Yêu cầu đã được gửi và đang chờ nhân viên duyệt.")
            : new(false, null, "Không thể tạo yêu cầu từ phòng hoặc lịch xem này.");
    }

    public async Task<ReservationResult> ApproveAsync(int id, ApproveReservationRequest request, int actorId)
    {
        if (id <= 0 || actorId <= 0 || request.HanThanhToan <= DateTimeOffset.UtcNow ||
            request.HanKyHopDong <= request.HanThanhToan)
            return new(false, null, "Số tiền hoặc thời hạn không hợp lệ.");

        var approved = await store.TryApproveAsync(id,
            request.HanThanhToan.UtcDateTime, request.HanKyHopDong.UtcDateTime, actorId);
        return approved
            ? new(true, id, "Đã duyệt yêu cầu giữ chỗ.")
            : new(false, null, "Phòng hoặc yêu cầu đã thay đổi, hoặc bạn không có quyền duyệt.");
    }

    public Task<IReadOnlyList<ReservationDto>> GetMineAsync(int actorId) => store.GetMineAsync(actorId);
    public async Task<ReservationResult> CloseUnpaidAsync(int id, int actorId, string reason)
    {
        if (id <= 0 || actorId <= 0 || string.IsNullOrWhiteSpace(reason) || reason.Length > 500)
            return new(false, null, "Nhập lý do tối đa 500 ký tự.");
        var saved = await store.TryCloseUnpaidAsync(id, actorId, reason.Trim());
        return saved ? new(true, id, "Đã kết thúc yêu cầu chưa thanh toán.")
            : new(false, null, "Yêu cầu đã có minh chứng/tiền hoặc bạn không có quyền. Hãy xử lý ở đối chiếu/hoàn tiền.");
    }
    public Task<IReadOnlyList<ReservationDto>> GetQueueAsync(int actorId) => store.GetQueueAsync(actorId);
}
