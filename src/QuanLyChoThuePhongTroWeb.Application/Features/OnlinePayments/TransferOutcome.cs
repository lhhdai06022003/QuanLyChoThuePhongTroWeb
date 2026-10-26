using QuanLyChoThuePhongTroWeb.Application.Features.ReservationPayments;

namespace QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;

public enum TransferOutcomeKind
{
    // Đã ghi sổ.
    Recorded,
    // Giao dịch này đã được xử lý trước đó (kể cả dòng đã hủy ghi nhận).
    Duplicate,
    // Không phải tiền vào hoặc số tiền không dương.
    Ignored,
    // Tiền vào tài khoản khác tài khoản của hệ thống.
    WrongAccount,
    // Không lấy được mã chuyển khoản hợp lệ.
    Unmatched,
    // Hóa đơn không tồn tại, đã hủy, chưa công bố hoặc có tiền lẻ.
    InvoiceNotPayable,
    // Hóa đơn đã thu đủ.
    AlreadyPaid,
    // Lỗi hệ thống khi ghi sổ; người gọi nên để SePay gửi lại (an toàn nhờ chống trùng).
    Failed
}

public sealed record TransferOutcome
{
    public required TransferOutcomeKind Kind { get; init; }
    public required IncomingBankTransfer Transfer { get; init; }
    public string DedupKey => Transfer.DedupKey;

    // Mã đã chuẩn hóa, null nếu chưa tách được.
    public string? Code { get; init; }
    public int? HoaDonId { get; init; }
    public string? MaHoaDon { get; init; }
    // Chỉ có khi Recorded.
    public int? LichSuThanhToanId { get; init; }
    // Chỉ có khi Recorded: tài khoản khách thuê đứng tên hợp đồng, null nếu khách chưa có tài khoản hoạt động.
    public int? TenantUserId { get; init; }
    public decimal SoTienGhiNhan { get; init; }
    public decimal TienThua { get; init; }
    public decimal TongTien { get; init; }
    // Số đã thu và số còn lại sau lần này.
    public decimal DaThu { get; init; }
    public decimal ConLai { get; init; }
    public bool DaThanhToanDu => Kind == TransferOutcomeKind.Recorded && ConLai == 0;
    // InvoiceStatusLabels.ThanhToan(...) của hóa đơn sau lần này.
    public string? TrangThaiThanhToan { get; init; }
    // Mã lượt chờ thanh toán đã bị hủy vì đã nhận chuyển khoản.
    public string? MaLuotDaHuy { get; init; }
    // Hóa đơn còn lượt khách đã nộp minh chứng chờ nhân viên đối chiếu: cần kiểm trùng trước khi xác nhận.
    public bool ConMinhChungChoDoiChieu { get; init; }
    // Chỉ có khi giao dịch giữ chỗ được ghi (Kind == Recorded, mã TTPTGC). Khi đó HoaDonId luôn null.
    public SystemReservationRecordResult? GiuCho { get; init; }
    // Lý do của InvoiceNotPayable và Failed, để log và thông báo.
    public string? LyDo { get; init; }
}
