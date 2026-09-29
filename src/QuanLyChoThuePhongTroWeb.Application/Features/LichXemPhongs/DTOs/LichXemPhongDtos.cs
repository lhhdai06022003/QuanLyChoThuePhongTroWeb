namespace QuanLyChoThuePhongTroWeb.Application.Features.LichXemPhongs.DTOs;

public sealed record ViewingSlotDto(int Id, DateTime BatDauUtc, DateTime KetThucUtc, int SoChoConLai);

public sealed record CreateViewingRequest(string MaCongKhai, int? KhungGioXemPhongId,
    DateTimeOffset ThoiGianMongMuon, string HoTen, string SoDienThoai, string? Email, string? GhiChu);

public sealed record CreateViewingCommand(string MaCongKhai, int? KhungGioXemPhongId,
    DateTime ThoiGianMongMuonUtc, string HoTen, string SoDienThoai, string? Email, string? GhiChu,
    int? NguoiDungId);

public sealed record ViewingSubmissionResult(bool Success, string Message);

public sealed record StaffViewingDto(int Id, string TenPhong, string TenChiNhanh,
    string HoTen, string SoDienThoai, string? Email, DateTime ThoiGianMongMuonUtc,
    string? GhiChu, string TrangThai);

public sealed record GuestViewingDto(int Id, string TenPhong, string TenChiNhanh,
    string? MaCongKhai, DateTime ThoiGianMongMuonUtc, string TrangThai);
