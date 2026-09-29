using QuanLyChoThuePhongTroWeb.Application.Common.Files;
using QuanLyChoThuePhongTroWeb.Application.Features.GiuChos.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.GiuChos.Persistence;

namespace QuanLyChoThuePhongTroWeb.Application.Features.GiuChos.Services;

public sealed class ThanhToanGiuChoService(IThanhToanGiuChoStore store,
    IReservationProofStorage storage) : IThanhToanGiuChoService
{
    public Task<HoldPaymentDto?> GetForGuestAsync(int reservationId, int actorId) =>
        store.GetForGuestAsync(reservationId, actorId);

    public async Task<PaymentActionResult> SubmitProofAsync(int paymentId, int actorId,
        SubmitProofRequest request, UploadFile image)
    {
        if (paymentId <= 0 || actorId <= 0 || request.SoTienKhaiBao <= 0 ||
            request.NgayChuyenTien > DateTimeOffset.UtcNow.AddMinutes(5) ||
            request.MaGiaoDichNganHang?.Length > 100 || request.GhiChu?.Length > 500 ||
            image.Length is <= 0 or > 5_242_880)
            return new(false, "Thông tin chuyển khoản hoặc ảnh không hợp lệ.");
        if (!await store.CanSubmitAsync(paymentId, actorId, DateTime.UtcNow))
            return new(false, "Yêu cầu thanh toán không còn nhận minh chứng.");

        string key;
        try { key = await storage.SaveAsync(image); }
        catch (InvalidDataException) { return new(false, "Chỉ chấp nhận ảnh PNG, JPEG hoặc WebP tối đa 5 MB."); }

        try
        {
            var saved = await store.TrySubmitAsync(paymentId, actorId, key, request, DateTime.UtcNow);
            if (saved) return new(true, "Đã gửi minh chứng. Phòng được giữ trong lúc nhân viên đối chiếu.");
            await storage.DeleteAsync(key);
            return new(false, "Yêu cầu vừa thay đổi. Vui lòng tải lại trang.");
        }
        catch
        {
            await storage.DeleteAsync(key);
            throw;
        }
    }

    public async Task<StoredReservationProof?> ReadProofAsync(int proofId, int actorId)
    {
        var key = await store.GetAuthorizedProofKeyAsync(proofId, actorId);
        return key is null ? null : await storage.ReadAsync(key);
    }

    public Task<IReadOnlyList<ProofReviewDto>> GetReviewQueueAsync(int actorId) =>
        store.GetReviewQueueAsync(actorId);

    public async Task<PaymentActionResult> ConfirmAsync(int proofId, int actorId,
        string bankReference, decimal actualAmount, DateTimeOffset receivedAt)
    {
        if (proofId <= 0 || actorId <= 0 || actualAmount <= 0 ||
            string.IsNullOrWhiteSpace(bankReference) || bankReference.Length > 100 ||
            receivedAt > DateTimeOffset.UtcNow.AddMinutes(5))
            return new(false, "Thông tin đối chiếu không hợp lệ.");
        var confirmed = await store.ConfirmAsync(proofId, actorId,
            bankReference.Trim(), actualAmount, receivedAt.UtcDateTime);
        return confirmed
            ? new(true, "Đã xác nhận tiền giữ chỗ.")
            : new(false, "Không thể xác nhận: kiểm tra số tiền, thời điểm nhận hoặc quyền xử lý.");
    }

    public async Task<PaymentActionResult> RejectAsync(int proofId, int actorId, string reason,
        decimal actualAmount, string? bankReference, DateTimeOffset? receivedAt)
    {
        if (proofId <= 0 || actorId <= 0 || string.IsNullOrWhiteSpace(reason) ||
            reason.Length > 500 || actualAmount < 0 ||
            receivedAt > DateTimeOffset.UtcNow.AddMinutes(5) ||
            (actualAmount > 0 && (string.IsNullOrWhiteSpace(bankReference) ||
                bankReference.Length > 100 || receivedAt is null)))
            return new(false, "Vui lòng nhập lý do và thông tin tiền thực nhận.");
        var rejected = await store.RejectAsync(proofId, actorId, reason.Trim(),
            actualAmount, bankReference?.Trim(), receivedAt?.UtcDateTime);
        return rejected
            ? new(true, "Đã từ chối minh chứng và nhả phòng. Khoản tiền thực nhận vẫn được ghi nhận để xử lý hoàn.")
            : new(false, "Không thể từ chối minh chứng này.");
    }
}
