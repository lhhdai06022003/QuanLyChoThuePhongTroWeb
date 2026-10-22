using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Logging;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Security;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Services;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;

namespace QuanLyChoThuePhongTroWeb.Application.Features.Viewings;

public sealed class ViewingAdminRequestService(IViewingAdminRequestStore store,
    IEmployeeAccessService employeeAccess, TimeProvider clock, IEmailService email,
    ILogger<ViewingAdminRequestService> logger) : IViewingAdminRequestService
{
    public async Task<IReadOnlyList<ViewingAdminRequestDto>> ListAsync(int actorId,
        CancellationToken cancellationToken = default)
    {
        var scope = await employeeAccess.GetScopeAsync(actorId, cancellationToken);
        return scope is null ? [] : await store.ListAsync(scope.AllowedBranchIds, cancellationToken);
    }

    public async Task<ServiceResult> UpdateAsync(int actorId, int requestId,
        ViewingAdminAction action, DateTime? confirmedTimeUtc, string? reason,
        CancellationToken cancellationToken = default)
    {
        var nowUtc = clock.GetUtcNow().UtcDateTime;
        if (actorId <= 0 || requestId <= 0 || !Enum.IsDefined(action) ||
            action == ViewingAdminAction.Confirm &&
                (confirmedTimeUtc is null || confirmedTimeUtc.Value.Kind != DateTimeKind.Utc ||
                 confirmedTimeUtc.Value <= nowUtc) ||
            reason?.Length > 500 ||
            action == ViewingAdminAction.Reject && string.IsNullOrWhiteSpace(reason))
            return ServiceResult.Fail("Thông tin xử lý lịch xem không hợp lệ.");
        var branchId = await store.GetBranchIdAsync(requestId, cancellationToken);
        if (!branchId.HasValue) return ServiceResult.NotFound("Không tìm thấy lịch xem.");
        var scope = await employeeAccess.GetScopeAsync(actorId, cancellationToken);
        if (scope is null || !scope.CanAccessBranch(branchId.Value))
            return ServiceResult.Forbidden("Bạn không có quyền xử lý lịch này.");
        var cancellationReason = action == ViewingAdminAction.CancelBecauseRented
            ? "Phòng đã có người thuê." : reason?.Trim();
        var error = await store.UpdateAsync(actorId, requestId, action,
            confirmedTimeUtc, cancellationReason, nowUtc, cancellationToken);
        if (error != ViewingAdminError.None)
            return error switch
            {
                ViewingAdminError.InvalidState => ServiceResult.Fail("Lịch xem đã thay đổi hoặc không còn xử lý được."),
                ViewingAdminError.Conflict => ServiceResult.Fail("Lịch xem vừa thay đổi, vui lòng tải lại."),
                _ => ServiceResult.NotFound("Không tìm thấy lịch xem.")
            };

        var expectedStatus = action switch
        {
            ViewingAdminAction.Confirm => "DaXacNhanLich",
            ViewingAdminAction.Reject => "TuChoi",
            ViewingAdminAction.CancelBecauseRented => "DaHuy",
            _ => null
        };
        if (expectedStatus is null) return ServiceResult.Ok("Đã cập nhật lịch xem.");

        // The store commits before returning. Email failure must not undo the customer's status.
        var sent = false;
        try
        {
            var notice = await store.GetNotificationAsync(requestId, cancellationToken);
            if (notice?.Status == expectedStatus && notice.BranchId == branchId.Value)
                sent = await DeliverAsync(notice);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Không gửi được thông báo sau cập nhật lịch xem {RequestId}", requestId);
        }
        var message = action switch
        {
            ViewingAdminAction.Confirm => "Đã xác nhận lịch xem.",
            ViewingAdminAction.Reject => "Đã từ chối lịch xem.",
            _ => "Đã hủy lịch xem vì phòng đã có người thuê."
        };
        return ServiceResult.Ok(message + (sent ? " Đã gửi email cho khách."
            : " Tuy nhiên, chưa gửi được email. Kiểm tra cấu hình SMTP và dùng nút Gửi lại email; trạng thái lịch đã được lưu."),
            new ViewingEmailDelivery(sent));
    }

    public async Task<ServiceResult> ResendNotificationAsync(int actorId, int requestId,
        CancellationToken cancellationToken = default)
    {
        if (actorId <= 0 || requestId <= 0) return ServiceResult.NotFound("Không tìm thấy lịch xem.");
        var branchId = await store.GetBranchIdAsync(requestId, cancellationToken);
        if (!branchId.HasValue) return ServiceResult.NotFound("Không tìm thấy lịch xem.");
        var scope = await employeeAccess.GetScopeAsync(actorId, cancellationToken);
        if (scope is null || !scope.CanAccessBranch(branchId.Value))
            return ServiceResult.Forbidden("Bạn không có quyền gửi email cho lịch này.");
        var notice = await store.GetNotificationAsync(requestId, cancellationToken);
        if (notice is null || notice.BranchId != branchId.Value ||
            notice.Status is not ("DaXacNhanLich" or "TuChoi" or "DaHuy"))
            return ServiceResult.Fail("Lịch hiện chưa có kết quả xác nhận, từ chối hoặc hủy để gửi email.");
        return await DeliverAsync(notice)
            ? ServiceResult.Ok("Đã gửi email thông báo trạng thái hiện tại cho khách.", new ViewingEmailDelivery(true))
            : ServiceResult.Fail("Chưa gửi được email. Kiểm tra địa chỉ nhận và cấu hình SMTP rồi thử lại.");
    }

    private async Task<bool> DeliverAsync(ViewingAdminRequestDto notice)
    {
        if (string.IsNullOrWhiteSpace(notice.Email) || !new EmailAddressAttribute().IsValid(notice.Email))
            return false;
        try
        {
            var delivery = await email.SendViewingStatusNoticeAsync(notice);
            if (!delivery.IsSuccess)
                logger.LogWarning("Email thông báo lịch xem {RequestId} chưa gửi được: {Error}", notice.Id, delivery.ErrorMessage);
            return delivery.IsSuccess;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Lỗi gửi email thông báo lịch xem {RequestId}", notice.Id);
            return false;
        }
    }
}
