using System.Net.Mail;
using QuanLyChoThuePhongTroWeb.Application.Features.LichXemPhongs.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.LichXemPhongs.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Features.PhongCongKhais.Services;

namespace QuanLyChoThuePhongTroWeb.Application.Features.LichXemPhongs.Services;

public sealed class LichXemPhongService(IPhongCongKhaiService rooms, ILichXemPhongStore store)
    : ILichXemPhongService
{
    public Task<IReadOnlyList<StaffViewingDto>> GetStaffQueueAsync(int actorId) =>
        store.GetStaffQueueAsync(actorId);

    public Task<IReadOnlyList<ViewingSlotDto>> GetRescheduleSlotsAsync(int requestId, int actorId) =>
        requestId <= 0 || actorId <= 0
            ? Task.FromResult<IReadOnlyList<ViewingSlotDto>>(Array.Empty<ViewingSlotDto>())
            : store.GetRescheduleSlotsAsync(requestId, actorId, DateTime.UtcNow);

    public Task<IReadOnlyList<GuestViewingDto>> GetMineAsync(int actorId) =>
        store.GetMineAsync(actorId);

    public Task<bool> DecideAsync(int requestId, int actorId, bool confirm) =>
        store.DecideAsync(requestId, actorId, confirm);

    public Task<bool> ManageConfirmedAsync(int requestId, int actorId, ManageViewingRequest request)
    {
        if (requestId <= 0 || actorId <= 0 || !Enum.IsDefined(request.Action) ||
            string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > 500 ||
            request.NewSlotId is <= 0 ||
            (request.Action == ViewingStaffAction.Reschedule && request.NewSlotId is null &&
                (request.NewTimeUtc is null || request.NewTimeUtc <= DateTime.UtcNow)))
            return Task.FromResult(false);
        return store.ManageConfirmedAsync(requestId, actorId, request with { Reason = request.Reason.Trim() });
    }

    public async Task<IReadOnlyList<ViewingSlotDto>> GetOpenSlotsAsync(string code)
    {
        if (await rooms.GetAsync(code) is null) return Array.Empty<ViewingSlotDto>();
        return await store.GetOpenSlotsAsync(code, DateTime.UtcNow);
    }

    public async Task<ViewingSubmissionResult> CreateAsync(CreateViewingRequest request, int? actorId = null)
    {
        if (string.IsNullOrWhiteSpace(request.MaCongKhai) || await rooms.GetAsync(request.MaCongKhai) is null)
            return new(false, "Phòng hiện không nhận lịch xem.");

        var name = request.HoTen?.Trim() ?? string.Empty;
        var phone = request.SoDienThoai?.Trim() ?? string.Empty;
        var email = request.Email?.Trim();
        var note = request.GhiChu?.Trim();
        if (name.Length is < 2 or > 100 || phone.Length is < 9 or > 20 ||
            !phone.All(char.IsDigit) || email?.Length > 150 || note?.Length > 1000 ||
            (email is { Length: > 0 } && !MailAddress.TryCreate(email, out _)))
            return new(false, "Vui lòng kiểm tra lại thông tin liên hệ.");

        var desiredUtc = request.ThoiGianMongMuon.UtcDateTime;
        if (request.KhungGioXemPhongId is int slotId)
        {
            var slots = await GetOpenSlotsAsync(request.MaCongKhai);
            var slot = slots.FirstOrDefault(item => item.Id == slotId && item.SoChoConLai > 0);
            if (slot is null) return new(false, "Khung giờ này không còn khả dụng.");
            desiredUtc = slot.BatDauUtc;
        }
        if (desiredUtc <= DateTime.UtcNow)
            return new(false, "Thời gian xem phòng phải ở tương lai.");

        var accepted = await store.CreateAsync(new CreateViewingCommand(request.MaCongKhai,
            request.KhungGioXemPhongId, desiredUtc, name, phone, email, note, actorId));
        return accepted
            ? new(true, request.KhungGioXemPhongId is null
                ? "Yêu cầu đã được gửi. Nhân viên sẽ liên hệ để xác nhận lịch xem."
                : "Khung giờ xem phòng đã được xác nhận.")
            : new(false, "Phòng hoặc khung giờ vừa thay đổi. Vui lòng chọn lại.");
    }
}
