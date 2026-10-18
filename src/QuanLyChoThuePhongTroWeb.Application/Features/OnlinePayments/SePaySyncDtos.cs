namespace QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;

// Yêu cầu đồng bộ bù (spec thanh toán trực tuyến §4.3): khoảng thời gian theo UTC, hai đầu đều tính.
public sealed record SePaySyncRequest(DateTime TuUtc, DateTime DenUtc);

public sealed class SePaySyncSettingsDto
{
    public bool DaCauHinhApiToken { get; init; }
    // SePaySyncService.DefaultRangeDays
    public int SoNgayMacDinh { get; init; }
    // SePaySyncService.MaxRangeDays
    public int SoNgayToiDa { get; init; }
    // !SePayOptions.SyncRecordEnabled: màn chỉ đối chiếu với sổ, không ghi sổ và không thông báo.
    public bool ChiDoiChieu { get; init; }
}

public enum SePaySyncRowStatus
{
    MoiGhiNhan,
    DaCo,
    KhongKhopMa,
    HoaDonKhongNhanTien,
    ThieuMaThamChieu,
    BoQua,
    LoiGhiSo,
    // Chỉ khi chế độ chỉ đối chiếu: giao dịch có mã tham chiếu nhưng sổ chưa có khóa SEPAY-{mã}.
    ChuaGhiSo
}

public sealed class SePaySyncRow
{
    // id (UUID) của API, chỉ dùng nội bộ để bỏ giao dịch lặp; Web không trả ra ngoài.
    public string SePayApiId { get; init; } = string.Empty;
    public DateTime NgayGiaoDichUtc { get; init; }
    public decimal SoTien { get; init; }
    public string? NoiDung { get; init; }
    public string? MaThamChieu { get; init; }
    // TransferCodeParser.Extract(code, content, null)
    public string? MaChuyenKhoan { get; init; }
    public int? HoaDonId { get; init; }
    public string? MaHoaDon { get; init; }
    public SePaySyncRowStatus TrangThai { get; init; }
    public string NhanTrangThai { get; init; } = string.Empty;
    public string? GhiChu { get; init; }
}

public sealed record SePaySyncStatusCount(SePaySyncRowStatus TrangThai, string Nhan, int SoLuong);

public sealed class SePaySyncResult
{
    public IReadOnlyList<SePaySyncRow> Rows { get; init; } = Array.Empty<SePaySyncRow>();
    // Đủ mọi giá trị của SePaySyncRowStatus, kể cả số lượng 0.
    public IReadOnlyList<SePaySyncStatusCount> TongTheoTrangThai { get; init; } = Array.Empty<SePaySyncStatusCount>();
    // false: dừng giữa chừng (lỗi trang N hoặc quá số trang tối đa), LoiDongBo cho biết lý do.
    public bool HoanTat { get; init; }
    public string? LoiDongBo { get; init; }
    // Ví dụ "{n} giao dịch SePay trả về sai định dạng, không xử lý."
    public string? CanhBao { get; init; }
    public bool ChiDoiChieu { get; init; }
}
