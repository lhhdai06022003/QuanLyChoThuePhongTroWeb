namespace QuanLyChoThuePhongTroWeb.Application.Features.ReservationPayments;

// Ghi tiền giữ chỗ do hệ thống tự nhận (webhook SePay, IPN VNPay). Tách khỏi IReservationPaymentStore vì không có actorId
// và không kiểm chi nhánh: người gọi (ReservationTransferHandler) chỉ được dùng sau khi chữ ký/HMAC của kênh đã được kiểm.
public interface IReservationAutoPaymentStore
{
    // Có YeuCauThanhToanGiuCho với MaYeuCau đúng bằng maYeuCau (TTPTGC{id}) không. Chỉ đọc.
    Task<bool> PaymentRequestExistsAsync(string maYeuCau, CancellationToken cancellationToken = default);

    // Ghi một dòng GiaoDichGiuCho do hệ thống tự ghi (NguoiXacNhanId null) và chuyển trạng thái như ConfirmReceivedAsync.
    Task<SystemReservationRecordResult> RecordSystemTransferAsync(string maYeuCau, string maGiaoDich,
        decimal soTienThucNhan, DateTime ngayThucNhanUtc, string lyDoLichSu, DateTime nowUtc,
        CancellationToken cancellationToken = default);

    // Đọc thông tin giữ chỗ để khách thanh toán (actorId là KhachVangLai.NguoiDungId). Trả về null nếu không tồn tại hoặc không thuộc khách.
    Task<ReservationPaymentSnapshot?> GetGuestPaymentSnapshotAsync(int reservationId, int actorId, CancellationToken cancellationToken = default);

    // Tìm dòng giao dịch giữ chỗ theo khóa đối soát (VNPAY-..., SEPAY-...).
    Task<ReservationTransactionSnapshot?> FindActiveTransactionAsync(int reservationId, string ledgerKey, CancellationToken cancellationToken = default);

    // Kiểm tra dòng giao dịch giữ chỗ có MaGiaoDich trùng khóa dedupKey (SEPAY-..., VNPAY-...) không. Dùng đối chiếu màn SePay (A8).
    Task<bool> ExistsTransactionByDedupKeyAsync(string dedupKey, CancellationToken cancellationToken = default);
}
