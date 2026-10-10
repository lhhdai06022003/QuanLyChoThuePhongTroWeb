using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;

namespace QuanLyChoThuePhongTroWeb.Application.Features.Viewings;

public sealed class ViewingRequestService(IViewingRequestStore store, TimeProvider clock)
    : IViewingRequestService
{
    public Task<IReadOnlyList<ViewingSlotDto>> ListAvailableSlotsAsync(int roomId,
        CancellationToken cancellationToken = default) =>
        store.ListAvailableSlotsAsync(roomId, clock.GetUtcNow().UtcDateTime,
            cancellationToken);

    public Task<IReadOnlyList<ViewingRequestDto>> ListMineAsync(int actorId,
        CancellationToken cancellationToken = default) =>
        store.ListMineAsync(actorId, clock.GetUtcNow().UtcDateTime, cancellationToken);

    public async Task<ServiceResult> CancelAsync(int actorId, int requestId,
        CancellationToken cancellationToken = default)
    {
        if (actorId <= 0 || requestId <= 0)
            return ServiceResult.NotFound("Không tìm thấy lịch xem.");

        var error = await store.CancelAsync(actorId, requestId,
            clock.GetUtcNow().UtcDateTime, cancellationToken);
        return error switch
        {
            ViewingCancelError.None => ServiceResult.Ok("Đã hủy lịch xem."),
            ViewingCancelError.NotFound => ServiceResult.NotFound("Không tìm thấy lịch xem."),
            _ => ServiceResult.Fail("Không thể hủy lịch đã qua giờ hoặc đã kết thúc.")
        };
    }

    public async Task<ServiceResult<int>> SubmitAsync(int actorId, ViewingRequestInput input,
        CancellationToken cancellationToken = default)
    {
        var nowUtc = clock.GetUtcNow().UtcDateTime;
        if (actorId <= 0 || input is null || input.RoomId <= 0 ||
            input.SlotId.HasValue == input.PreferredTimeUtc.HasValue ||
            input.SlotId is <= 0 ||
            input.PreferredTimeUtc is { } preferred &&
                (preferred.Kind != DateTimeKind.Utc || preferred <= nowUtc) ||
            string.IsNullOrWhiteSpace(input.FullName) || input.FullName.Trim().Length > 100 ||
            string.IsNullOrWhiteSpace(input.Phone) ||
                !Regex.IsMatch(input.Phone.Trim(), "^\\+?[0-9]{9,15}$") ||
            string.IsNullOrWhiteSpace(input.Email) || input.Email.Trim().Length > 150 ||
                !new EmailAddressAttribute().IsValid(input.Email.Trim()) ||
            input.Note?.Length > 1000)
            return ServiceResult<int>.Fail("Thông tin lịch xem không hợp lệ.");

        var result = await store.CreateAsync(actorId, input, nowUtc,
            cancellationToken);
        if (result.RequestId.HasValue)
            return ServiceResult<int>.Ok(result.RequestId.Value);

        return result.Error switch
        {
            ViewingCreateError.GuestMissing => ServiceResult<int>.Forbidden("Không tìm thấy hồ sơ khách hợp lệ cho tài khoản. Vui lòng liên hệ nhân viên."),
            ViewingCreateError.RoomUnavailable => ServiceResult<int>.NotFound("Phòng không còn được đăng công khai."),
            ViewingCreateError.SlotUnavailable => ServiceResult<int>.Fail("Khung giờ không còn khả dụng."),
            ViewingCreateError.SlotFull => ServiceResult<int>.Fail("Khung giờ đã hết chỗ."),
            ViewingCreateError.Conflict => ServiceResult<int>.Fail("Lịch xem vừa thay đổi, vui lòng thử lại."),
            _ => ServiceResult<int>.Fail("Không thể tạo lịch xem phòng.")
        };
    }
}
