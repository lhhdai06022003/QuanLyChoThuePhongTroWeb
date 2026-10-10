using Microsoft.Extensions.Logging;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Security;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Services;
using QuanLyChoThuePhongTroWeb.Application.Common.Configurations;
using QuanLyChoThuePhongTroWeb.Application.Common.Files;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;

namespace QuanLyChoThuePhongTroWeb.Application.Features.ReservationPayments;

public sealed class ReservationPaymentService(IReservationPaymentStore store,
    IEmployeeAccessService employeeAccess, TimeProvider clock,
    IMeterImageStorageService storage, InvoicePaymentOptions options,
    ILogger<ReservationPaymentService> logger) : IReservationPaymentService
{
    private const string ProofFolder = "hold-payment-proofs";

    public async Task<ServiceResult<int>> SubmitEvidenceAsync(int actorId,
        EvidenceSubmission submission, CancellationToken cancellationToken = default)
    {
        if (actorId <= 0 || submission.PaymentRequestId <= 0 ||
            submission.ClaimedAmount <= 0 ||
            decimal.Round(submission.ClaimedAmount, 2) != submission.ClaimedAmount ||
            submission.TransferTimeUtc.Kind != DateTimeKind.Utc ||
            submission.TransferTimeUtc > clock.GetUtcNow().UtcDateTime.AddMinutes(5) ||
            submission.BankReference?.Length > 100 || submission.Note?.Length > 500)
            return ServiceResult<int>.Fail("Minh chứng chuyển tiền không hợp lệ.");

        var fileError = ProofImageValidator.Validate(submission.Image, options.MinhChungToiDaBytes);
        if (fileError is not null)
            return ServiceResult<int>.Fail(fileError);

        // Ảnh minh chứng lưu trên Cloudinary như minh chứng hóa đơn, không lưu trên ổ đĩa server.
        var upload = submission.Image with
        {
            FileName = $"giu-cho-{submission.PaymentRequestId}" +
                Path.GetExtension(submission.Image.FileName).ToLowerInvariant()
        };
        MeterImageUploadResult uploaded;
        try
        {
            uploaded = await storage.UploadAsync(upload, ProofFolder, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Lỗi khi tải ảnh minh chứng giữ chỗ cho yêu cầu {PaymentRequestId}",
                submission.PaymentRequestId);
            return ServiceResult<int>.Fail("Không thể tải ảnh lên dịch vụ lưu trữ. Vui lòng thử lại.");
        }

        int? id;
        try
        {
            id = await store.AddEvidenceAsync(actorId,
                new EvidenceRecord(submission.PaymentRequestId, uploaded.Url,
                    uploaded.PublicId, submission.ClaimedAmount,
                    submission.TransferTimeUtc, submission.BankReference, submission.Note),
                clock.GetUtcNow().UtcDateTime, cancellationToken);
        }
        catch
        {
            await CleanupUploadedAsync(uploaded.PublicId);
            throw;
        }

        if (!id.HasValue)
        {
            await CleanupUploadedAsync(uploaded.PublicId);
            return ServiceResult<int>.NotFound("Không tìm thấy yêu cầu thanh toán đang chờ của bạn.");
        }

        return ServiceResult<int>.Ok(id.Value, "Đã gửi ảnh. Nhân viên cần đối soát tiền thực nhận.");
    }

    private async Task CleanupUploadedAsync(string publicId)
    {
        try
        {
            await storage.DeleteAsync(publicId, CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Không thể dọn ảnh minh chứng giữ chỗ mồ côi {PublicId}", publicId);
        }
    }

    public async Task<ServiceResult> ConfirmReceivedAsync(int actorId,
        int paymentRequestId, int? evidenceId, string bankReference,
        decimal actualAmount, DateTime receivedAtUtc,
        CancellationToken cancellationToken = default)
    {
        if (paymentRequestId <= 0 || evidenceId is <= 0 ||
            string.IsNullOrWhiteSpace(bankReference) ||
            bankReference.Trim().Length > 100 || actualAmount <= 0 ||
            decimal.Round(actualAmount, 2) != actualAmount ||
            receivedAtUtc.Kind != DateTimeKind.Utc ||
            receivedAtUtc <= DateTime.UnixEpoch ||
            receivedAtUtc > clock.GetUtcNow().UtcDateTime.AddMinutes(5))
            return ServiceResult.Fail("Mã giao dịch, số tiền hoặc thời điểm thực nhận không hợp lệ.");
        var branchId = await store.GetPaymentBranchIdAsync(paymentRequestId,
            cancellationToken);
        if (!branchId.HasValue)
            return ServiceResult.NotFound("Không tìm thấy yêu cầu thanh toán.");
        var scope = await employeeAccess.GetScopeAsync(actorId, cancellationToken);
        if (scope is null || !scope.CanAccessBranch(branchId.Value))
            return ServiceResult.Forbidden("Bạn không có quyền đối soát phòng này.");

        var error = await store.ConfirmReceivedAsync(actorId, paymentRequestId,
            evidenceId, bankReference.Trim(), actualAmount, receivedAtUtc,
            clock.GetUtcNow().UtcDateTime, cancellationToken);
        return error switch
        {
            PaymentError.None => ServiceResult.Ok("Đã ghi nhận giao dịch thực nhận."),
            PaymentError.DuplicateTransaction => ServiceResult.Fail("Mã giao dịch hoặc minh chứng đã được xác nhận."),
            PaymentError.InvalidState => ServiceResult.Fail("Yêu cầu hoặc minh chứng không hợp lệ."),
            PaymentError.Conflict => ServiceResult.Fail("Giao dịch vừa thay đổi, vui lòng tải lại."),
            _ => ServiceResult.NotFound("Không tìm thấy yêu cầu thanh toán.")
        };
    }

    public Task<IReadOnlyList<PaymentEvidenceDto>> ListMineAsync(int actorId,
        CancellationToken cancellationToken = default) =>
        store.ListMineAsync(actorId, cancellationToken);

    public async Task<IReadOnlyList<PaymentEvidenceDto>> ListForReservationAsync(
        int actorId, int reservationId, int branchId,
        CancellationToken cancellationToken = default)
    {
        var scope = await employeeAccess.GetScopeAsync(actorId, cancellationToken);
        return scope is null || !scope.CanAccessBranch(branchId) ? [] :
            await store.ListForReservationAsync(reservationId, cancellationToken);
    }

    // Đọc ảnh qua server để kiểm quyền thay vì lộ URL Cloudinary; khách chủ yêu cầu hoặc nhân viên cùng chi nhánh.
    public async Task<ServiceResult<MeterImageReadResult>> GetEvidenceImageAsync(
        int actorId, int evidenceId, CancellationToken cancellationToken = default)
    {
        var evidence = await store.GetEvidenceAccessAsync(evidenceId, cancellationToken);
        if (evidence is null || actorId <= 0)
            return ServiceResult<MeterImageReadResult>.NotFound("Không tìm thấy ảnh.");
        if (evidence.GuestUserId != actorId)
        {
            var scope = await employeeAccess.GetScopeAsync(actorId, cancellationToken);
            if (scope is null || !scope.CanAccessBranch(evidence.BranchId))
                return ServiceResult<MeterImageReadResult>.NotFound("Không tìm thấy ảnh.");
        }

        try
        {
            return ServiceResult<MeterImageReadResult>.Ok(await storage.ReadAsync(
                evidence.ImagePublicId ?? string.Empty, evidence.ImageUrl, cancellationToken));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Không đọc được ảnh minh chứng giữ chỗ {EvidenceId}", evidenceId);
            return ServiceResult<MeterImageReadResult>.Fail("Không tải được ảnh minh chứng.");
        }
    }
}
