namespace QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;

public enum VnPayReturnStatus
{
    // Sai chữ ký, sai merchant, thiếu tham số hoặc hóa đơn không thuộc khách: không lộ gì về hóa đơn.
    KhongXacThuc,
    DaHuy,
    KhongThanhCong,
    DaGhiNhan,
    // VNPay báo thành công nhưng sổ chưa có dòng của giao dịch này (IPN chưa tới).
    DangGhiNhan,
    // VNPay báo thành công nhưng hóa đơn không còn nhận khoản này và sẽ không có dòng sổ (IPN trả 02, hoặc dòng đã bị hủy ghi nhận).
    KhongGhiNhan
}

// Dữ liệu trang kết quả VNPay (spec VNPay §4.3). Chỉ đọc; HoaDonId null khi KhongXacThuc.
public sealed record VnPayReturnResult
{
    public required VnPayReturnStatus TrangThai { get; init; }
    public int? HoaDonId { get; init; }
    // Chỉ KhongThanhCong: VnPayResponseCodes.Describe(vnp_ResponseCode).
    public string? MoTaLoi { get; init; }
    // Chỉ DaGhiNhan.
    public int? LichSuThanhToanId { get; init; }
    // Chỉ DaGhiNhan: số đã ghi sổ của dòng VNPAY-{vnp_TransactionNo}.
    public decimal? SoTien { get; init; }
    // Chỉ DaGhiNhan: vnp_Amount / 100 khi khác số đã ghi sổ (khách trả thừa so với số còn lại lúc ghi); ngược lại null.
    public decimal? SoTienVnPayBao { get; init; }

    public static VnPayReturnResult Unverified { get; } = new() { TrangThai = VnPayReturnStatus.KhongXacThuc };
}
