namespace QuanLyChoThuePhongTroWeb.Application.Features.GiuChos.DTOs;

public sealed record RefundCandidateDto(int ReservationId, string TenPhong, string TenChiNhanh,
    string KhachHang, string TrangThaiGiuCho, decimal SoTienThucNhan,
    decimal? SoTienDaDuyet, decimal SoTienDaHoan, string? TrangThaiHoan,
    string? LyDo, int? DecisionId);

public sealed record RefundDecisionRequest(decimal SoTienHoanDuyet, string LyDo);

public sealed record RecordRefundRequest(string MaGiaoDichHoan, decimal SoTienHoan,
    DateTimeOffset NgayHoan, string? GhiChu);

public sealed record RefundActionResult(bool Success, string Message);
