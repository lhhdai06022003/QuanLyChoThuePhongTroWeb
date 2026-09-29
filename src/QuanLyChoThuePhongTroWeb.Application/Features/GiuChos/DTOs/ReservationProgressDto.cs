namespace QuanLyChoThuePhongTroWeb.Application.Features.GiuChos.DTOs;

public sealed record ReservationProgressDto(int Id, string TenPhong, string TenChiNhanh,
    string TrangThai, decimal? SoTienGiuCho, decimal TienThucNhan,
    DateTime? HanThanhToanUtc, DateTime? HanKyHopDongUtc, string? LyDo,
    int? HopDongId, decimal DaApDungCoc, decimal DaCanTruHoaDon, decimal ConCanTru,
    IReadOnlyList<ReservationTimelineItem> LichSu);
public sealed record ReservationTimelineItem(DateTime ThoiDiemUtc, string TrangThai, string? LyDo);
