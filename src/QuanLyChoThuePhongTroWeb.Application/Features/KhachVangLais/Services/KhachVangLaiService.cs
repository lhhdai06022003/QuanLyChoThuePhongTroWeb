using System.Net.Mail;
using System.Text.RegularExpressions;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Security;
using QuanLyChoThuePhongTroWeb.Application.Features.KhachVangLais.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.KhachVangLais.Persistence;

namespace QuanLyChoThuePhongTroWeb.Application.Features.KhachVangLais.Services;

public sealed class KhachVangLaiService(IKhachVangLaiStore store, IPasswordService passwords)
    : IKhachVangLaiService
{
    public Task<GuestProfileDto?> GetProfileAsync(int actorId) => store.GetProfileAsync(actorId);

    public async Task<GuestRegistrationResult> UpdateIdentityAsync(int actorId,
        UpdateGuestIdentityRequest request)
    {
        var email = request.Email?.Trim() ?? string.Empty;
        var cccd = request.CCCD?.Trim() ?? string.Empty;
        if (actorId <= 0 || email.Length is < 3 or > 150 ||
            !MailAddress.TryCreate(email, out _) || cccd.Length != 12 || !cccd.All(char.IsDigit))
            return new(false, null, "Email hoặc CCCD chưa hợp lệ.");
        var saved = await store.UpdateIdentityAsync(actorId, email, cccd);
        return saved ? new(true, actorId, "Đã cập nhật hồ sơ.")
            : new(false, null, "Không thể cập nhật hồ sơ.");
    }

    public async Task<GuestRegistrationResult> RegisterAsync(GuestRegistrationRequest request)
    {
        var username = request.TenDangNhap?.Trim().ToLowerInvariant() ?? string.Empty;
        var name = request.HoTen?.Trim() ?? string.Empty;
        var phone = request.SoDienThoai?.Trim() ?? string.Empty;
        var email = request.Email?.Trim();
        var cccd = request.CCCD?.Trim() ?? string.Empty;
        if (!Regex.IsMatch(username, "^[a-z0-9._]{4,50}$") ||
            request.MatKhau is null || request.MatKhau.Length is < 8 or > 100 ||
            name.Length is < 2 or > 100 || phone.Length is < 9 or > 20 ||
            !phone.All(char.IsDigit) || email is null || email.Length is < 3 or > 150 ||
            !MailAddress.TryCreate(email, out _) || cccd.Length != 12 || !cccd.All(char.IsDigit))
            return new(false, null, "Thông tin đăng ký chưa hợp lệ.");

        var hash = passwords.HashPassword(request.MatKhau);
        var id = await store.TryRegisterAsync(username, hash, name, phone, email, cccd);
        return id is int userId
            ? new(true, userId, "Tài khoản đã được tạo.")
            : new(false, null, "Tên đăng nhập đã được sử dụng.");
    }
}
