namespace QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;

// Biên lai web của một dòng sổ thanh toán (spec thanh toán trực tuyến §3.4). Thời gian giữ UTC, Web đổi sang giờ Việt Nam.
// Đợt, lũy kế, còn nợ và tiền thừa tính lại mỗi lần đọc, cùng công thức với email biên lai.
public sealed class InvoiceReceiptDto
{
    public int LichSuThanhToanId { get; init; }
    public int HoaDonId { get; init; }
    public string MaHoaDon { get; init; } = string.Empty;
    public string TenChiNhanh { get; init; } = string.Empty;
    public string DiaChiChiNhanh { get; init; } = string.Empty;
    public string TenPhong { get; init; } = string.Empty;
    public string TenNguoiThanhToan { get; init; } = string.Empty;
    public int Thang { get; init; }
    public int Nam { get; init; }
    public DateTime NgayThanhToanUtc { get; init; }
    public string PhuongThuc { get; init; } = string.Empty;
    public string MaGiaoDich { get; init; } = string.Empty;
    // Mã tham chiếu ngân hàng: chỉ có với dòng SePay ghi theo mã tham chiếu.
    public string? MaThamChieu { get; init; }
    public decimal TongTien { get; init; }
    public decimal SoTienLanNay { get; init; }
    public decimal LuyKeDaThanhToan { get; init; }
    // max(0, TongTien - LuyKeDaThanhToan).
    public decimal ConNo { get; init; }
    // Từ thẻ [TIEN-THUA:] trong ghi chú của dòng sổ.
    public decimal TienThua { get; init; }
    // LuyKeDaThanhToan >= TongTien.
    public bool DaThuDu { get; init; }
    public int DotThu { get; init; }
}
