namespace QuanLyChoThuePhongTroWeb.Application.Features.ReservationPayments;

public enum SystemReservationRecordStatus { Recorded, Duplicate, NotFound, Conflict }

// Chỉ Recorded mới có số liệu; các trạng thái khác chỉ có Status.
public sealed record SystemReservationRecordResult(SystemReservationRecordStatus Status)
{
    public int YeuCauGiuChoId { get; init; }
    public int YeuCauThanhToanGiuChoId { get; init; }
    public int GiaoDichGiuChoId { get; init; }
    public string MaYeuCau { get; init; } = string.Empty;
    public int PhongTroId { get; init; }
    public int ChiNhanhId { get; init; }
    public string TenChiNhanh { get; init; } = string.Empty;
    public string SoPhong { get; init; } = string.Empty;
    // KhachVangLai.NguoiDungId.
    public int GuestUserId { get; init; }
    public string TenKhach { get; init; } = string.Empty;
    public string? EmailKhach { get; init; }
    // YeuCauGiuCho.SoTienGiuCho ?? YeuCauThanhToanGiuCho.SoTien.
    public decimal SoTienGiuCho { get; init; }
    public decimal DaNhanTruoc { get; init; }
    public decimal DaNhanSau { get; init; }
    // Dòng vừa ghi có GhiChu LATE_REFUND_REQUIRED.
    public bool CanHoanTien { get; init; }
    // Giữ chỗ chuyển sang DangGiuCho trong lần này.
    public bool VuaCoHieuLuc { get; init; }
    // Tên enum TrangThaiYeuCauGiuCho sau lần này.
    public string TrangThaiGiuCho { get; init; } = string.Empty;
    // YeuCauThanhToanGiuCho.HanThanhToan (UTC).
    public DateTime HanThanhToanUtc { get; init; }
    public DateTime? HanKyHopDongUtc { get; init; }
    public bool ConMinhChungChoXacNhan { get; init; }
}
