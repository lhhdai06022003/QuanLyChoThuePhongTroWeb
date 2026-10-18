namespace QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;

// Panel thanh toán của khách thuê (spec thanh toán trực tuyến §3.2). Thời gian giữ UTC, Web đổi sang giờ Việt Nam.
public sealed class TenantPaymentPanelDto
{
    public int HoaDonId { get; init; }
    public string MaHoaDon { get; init; } = string.Empty;
    public string TenPhong { get; init; } = string.Empty;
    public string TenChiNhanh { get; init; } = string.Empty;
    public int Thang { get; init; }
    public int Nam { get; init; }
    public decimal TongTien { get; init; }
    public decimal DaThu { get; init; }
    public decimal ConLai { get; init; }
    public DateTime? HanThanhToanUtc { get; init; }
    public bool CoTheThanhToan { get; init; }
    public string? LyDoKhongTheThanhToan { get; init; }
    // TTPTHD{HoaDonId}.
    public string MaChuyenKhoan { get; init; } = string.Empty;
    // BankName, trống thì BankId.
    public string TenNganHang { get; init; } = string.Empty;
    public string SoTaiKhoan { get; init; } = string.Empty;
    public string ChuTaiKhoan { get; init; } = string.Empty;
    // Rỗng khi !CoTheThanhToan.
    public IReadOnlyList<BankDeeplinkDto> Deeplinks { get; init; } = Array.Empty<BankDeeplinkDto>();
    public IReadOnlyList<TenantPaymentEntryDto> CacLanThanhToan { get; init; } = Array.Empty<TenantPaymentEntryDto>();
    // Minh chứng khách đã nộp trước khi gỡ tính năng nộp minh chứng và còn chờ nhân viên đối chiếu.
    public LegacyProofNoticeDto? MinhChungChoDoiChieu { get; init; }
}

public sealed record BankDeeplinkDto(string TenNganHang, string Url);

// DotThu đánh số từ 1 theo (NgayThanhToan, LichSuThanhToanId) trong các dòng sổ chưa xóa mềm.
public sealed record TenantPaymentEntryDto(int LichSuThanhToanId, int DotThu, decimal SoTien, DateTime NgayThanhToanUtc, string PhuongThuc);

public sealed record LegacyProofNoticeDto(int MinhChungId, decimal SoTien, DateTime NgayNopUtc);

public sealed record InvoiceQrImage(byte[] Png, string FileName);
