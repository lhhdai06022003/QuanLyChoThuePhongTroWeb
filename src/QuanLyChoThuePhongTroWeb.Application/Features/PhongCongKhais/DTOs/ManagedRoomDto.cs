namespace QuanLyChoThuePhongTroWeb.Application.Features.PhongCongKhais.DTOs;

public sealed record ManagedRoomDto(int Id, string SoPhong, string TenChiNhanh,
    decimal GiaThue, string TrangThai, bool DuocDangTin,
    string? TieuDe, string? MaCongKhai, int SoAnh);

public sealed record RoomManagementResult(bool Success, string Message);
