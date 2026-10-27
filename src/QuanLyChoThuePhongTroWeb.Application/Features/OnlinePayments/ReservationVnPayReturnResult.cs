namespace QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;

// Dữ liệu trang kết quả VNPay cho giữ chỗ (spec giữ chỗ §5.3, §15). Chỉ đọc; YeuCauGiuChoId null khi KhongXacThuc.
public sealed record ReservationVnPayReturnResult
{
    public required VnPayReturnStatus TrangThai { get; init; }
    public int? YeuCauGiuChoId { get; init; }
    // Chỉ KhongThanhCong: VnPayResponseCodes.Describe(vnp_ResponseCode).
    public string? MoTaLoi { get; init; }
    // Chỉ DaGhiNhan.
    public int? GiaoDichGiuChoId { get; init; }
    // Chỉ DaGhiNhan: số đã ghi sổ của dòng VNPAY-{vnp_TransactionNo}.
    public decimal? SoTien { get; init; }
    // Chỉ DaGhiNhan: vnp_Amount / 100 khi khác số đã ghi sổ; ngược lại null.
    public decimal? SoTienVnPayBao { get; init; }
    // Ghi chú nếu là LATE_REFUND_REQUIRED: khoản này cần hoàn.
    public bool CanHoan { get; init; }

    public static ReservationVnPayReturnResult Unverified { get; } = new() { TrangThai = VnPayReturnStatus.KhongXacThuc };
}
