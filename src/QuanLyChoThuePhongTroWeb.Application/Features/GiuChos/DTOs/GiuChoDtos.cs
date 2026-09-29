namespace QuanLyChoThuePhongTroWeb.Application.Features.GiuChos.DTOs;

public sealed record CreateReservationRequest(string MaCongKhai, int? YeuCauXemPhongId);
public sealed record ApproveReservationRequest(DateTimeOffset HanThanhToan,
    DateTimeOffset HanKyHopDong);
public sealed record ReservationResult(bool Success, int? Id, string Message);
public sealed record ReservationDto(int Id, string TenPhong, string TenChiNhanh,
    string KhachHang, string TrangThai, decimal GiaThue, decimal? SoTienGiuCho, DateTime? HanThanhToanUtc,
    DateTime? HanKyHopDongUtc, string? MaYeuCau, string? NoiDungChuyenKhoan);
