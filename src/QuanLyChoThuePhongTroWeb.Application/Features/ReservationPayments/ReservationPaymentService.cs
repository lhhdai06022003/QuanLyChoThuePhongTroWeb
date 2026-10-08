using QuanLyChoThuePhongTroWeb.Application.Abstractions.Security;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;

namespace QuanLyChoThuePhongTroWeb.Application.Features.ReservationPayments;

public sealed class ReservationPaymentService(IReservationPaymentStore store,
    IEmployeeAccessService employeeAccess, TimeProvider clock) : IReservationPaymentService
{
    public async Task<ServiceResult<int>> SubmitEvidenceAsync(int actorId,
        EvidenceSubmission submission, CancellationToken cancellationToken = default)
    {
        if (actorId <= 0 || submission.PaymentRequestId <= 0 ||
            !IsSafeFileName(submission.PrivateFileName) ||
            submission.ClaimedAmount <= 0 ||
            decimal.Round(submission.ClaimedAmount, 2) != submission.ClaimedAmount ||
            submission.TransferTimeUtc.Kind != DateTimeKind.Utc ||
            submission.TransferTimeUtc > clock.GetUtcNow().UtcDateTime.AddMinutes(5) ||
            submission.BankReference?.Length > 100 || submission.Note?.Length > 500)
            return ServiceResult<int>.Fail("Minh chứng chuyển tiền không hợp lệ.");

        var id = await store.AddEvidenceAsync(actorId, submission,
            clock.GetUtcNow().UtcDateTime, cancellationToken);
        return id.HasValue
            ? ServiceResult<int>.Ok(id.Value, "Đã gửi ảnh. Nhân viên cần đối soát tiền thực nhận.")
            : ServiceResult<int>.NotFound("Không tìm thấy yêu cầu thanh toán đang chờ của bạn.");
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

    public async Task<ServiceResult<PaymentEvidenceAccess>> GetEvidenceAccessAsync(
        int actorId, int evidenceId, CancellationToken cancellationToken = default)
    {
        var evidence = await store.GetEvidenceAccessAsync(evidenceId, cancellationToken);
        if (evidence is null || !IsSafeFileName(evidence.PrivateFileName))
            return ServiceResult<PaymentEvidenceAccess>.NotFound("Không tìm thấy ảnh.");
        if (evidence.GuestUserId == actorId)
            return ServiceResult<PaymentEvidenceAccess>.Ok(evidence);
        var scope = await employeeAccess.GetScopeAsync(actorId, cancellationToken);
        return scope is not null && scope.CanAccessBranch(evidence.BranchId)
            ? ServiceResult<PaymentEvidenceAccess>.Ok(evidence)
            : ServiceResult<PaymentEvidenceAccess>.NotFound("Không tìm thấy ảnh.");
    }

    private static bool IsSafeFileName(string name) =>
        !string.IsNullOrWhiteSpace(name) && name.Length <= 80 &&
        name.All(character => char.IsLetterOrDigit(character) || character is '.' or '-') &&
        (name.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
         name.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
         name.EndsWith(".webp", StringComparison.OrdinalIgnoreCase));
}
