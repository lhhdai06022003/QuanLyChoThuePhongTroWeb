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
}
