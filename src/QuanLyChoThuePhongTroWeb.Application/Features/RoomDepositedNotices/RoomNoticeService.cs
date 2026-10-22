using QuanLyChoThuePhongTroWeb.Application.Abstractions.Security;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Services;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;

namespace QuanLyChoThuePhongTroWeb.Application.Features.RoomDepositedNotices;

public sealed class RoomNoticeService(IRoomNoticeStore store,
    IEmployeeAccessService employeeAccess, IEmailService email,
    TimeProvider clock) : IRoomNoticeService
{
    public async Task<ServiceResult<RoomNoticePreview>> PreviewAsync(int actorId,
        int roomId, CancellationToken cancellationToken = default)
    {
        if (actorId <= 0 || roomId <= 0)
            return ServiceResult<RoomNoticePreview>.NotFound("Không tìm thấy phòng.");
        var preview = await store.GetPreviewAsync(roomId,
            clock.GetUtcNow().UtcDateTime, cancellationToken);
        if (preview is null)
            return ServiceResult<RoomNoticePreview>.Fail(
                "Phòng chưa có khoản giữ chỗ đã được đối soát đủ tiền.");
        var scope = await employeeAccess.GetScopeAsync(actorId, cancellationToken);
        return scope is not null && scope.CanAccessBranch(preview.BranchId)
            ? ServiceResult<RoomNoticePreview>.Ok(preview)
            : ServiceResult<RoomNoticePreview>.Forbidden(
                "Bạn không có quyền gửi email cho phòng này.");
    }

    public async Task<ServiceResult<IReadOnlyList<RoomNoticeDelivery>>> SendAsync(
        int actorId, int roomId, string recipientDigest,
        CancellationToken cancellationToken = default)
    {
        var previewResult = await PreviewAsync(actorId, roomId, cancellationToken);
        if (!previewResult.Success || previewResult.Data is null)
            return ServiceResult<IReadOnlyList<RoomNoticeDelivery>>.FailFrom(previewResult);
        var preview = previewResult.Data;
        if (string.IsNullOrWhiteSpace(recipientDigest) ||
            !string.Equals(preview.RecipientDigest, recipientDigest,
                StringComparison.Ordinal))
            return ServiceResult<IReadOnlyList<RoomNoticeDelivery>>.Fail(
                "Danh sách người nhận đã thay đổi. Vui lòng xem trước và xác nhận lại.");
        if (preview.Recipients.Count == 0)
            return ServiceResult<IReadOnlyList<RoomNoticeDelivery>>.Fail(
                "Hiện không có khách đang chờ để gửi thông báo.");

        var results = new List<RoomNoticeDelivery>();
        foreach (var recipient in preview.Recipients)
        {
            var result = await email.SendRoomDepositedNoticeAsync(recipient.Email,
                recipient.Name, preview.RoomNumber, preview.BranchName);
            results.Add(new RoomNoticeDelivery(recipient.Email,
                result.IsSuccess, result.IsSuccess ? null : result.ErrorMessage));
        }
        return ServiceResult<IReadOnlyList<RoomNoticeDelivery>>.Ok(results,
            $"Đã gửi {results.Count(item => item.Sent)}/{results.Count} email.");
    }
}
