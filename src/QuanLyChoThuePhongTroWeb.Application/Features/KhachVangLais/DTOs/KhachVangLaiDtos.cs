namespace QuanLyChoThuePhongTroWeb.Application.Features.KhachVangLais.DTOs;

public sealed record GuestRegistrationRequest(string TenDangNhap, string MatKhau,
    string HoTen, string SoDienThoai, string? Email, string CCCD);

public sealed record GuestRegistrationResult(bool Success, int? NguoiDungId, string Message);

public sealed record GuestProfileDto(string HoTen, string SoDienThoai, string? Email, string? CCCD);
public sealed record UpdateGuestIdentityRequest(string Email, string CCCD);
