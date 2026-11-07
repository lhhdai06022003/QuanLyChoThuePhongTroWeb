using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Security;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Application.Features.GuestAccounts;

public sealed class GuestRegistrationService(
    IGuestAccountStore store, IPasswordService passwordService) : IGuestRegistrationService
{
    public async Task<ServiceResult<int>> RegisterAsync(GuestRegistrationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
            return ServiceResult<int>.Fail("Vui lòng nhập thông tin đăng ký.");

        var username = request.Username?.Trim() ?? string.Empty;
        if (username.Length is < 3 or > 50)
            return ServiceResult<int>.Fail("Tên đăng nhập phải từ 3 đến 50 ký tự.");
        if (!Regex.IsMatch(username, @"\A[\p{L}\p{M}\p{Nd}._@+\-]+\z"))
            return ServiceResult<int>.Fail("Tên đăng nhập có thể dùng chữ, số và các dấu . _ @ + -, không có khoảng trắng.");

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length is < 8 or > 128)
            return ServiceResult<int>.Fail("Mật khẩu phải từ 8 đến 128 ký tự.");

        var fullName = request.FullName?.Trim() ?? string.Empty;
        if (fullName.Length is < 1 or > 100)
            return ServiceResult<int>.Fail("Vui lòng nhập họ tên, tối đa 100 ký tự.");

        var phone = Regex.Replace(request.Phone?.Trim() ?? string.Empty, @"[\p{Zs}().-]", "");
        if (!Regex.IsMatch(phone, @"\A\+?[0-9]{9,15}\z"))
            return ServiceResult<int>.Fail("Số điện thoại cần có 9 đến 15 chữ số, ví dụ 0912 345 678 hoặc +84 912 345 678.");

        var email = request.Email?.Trim() ?? string.Empty;
        if (email.Length is < 1 or > 150 || !new EmailAddressAttribute().IsValid(email))
            return ServiceResult<int>.Fail("Vui lòng nhập email hợp lệ, ví dụ ban@example.com.");

        var user = new NguoiDung
        {
            TenDangNhap = username,
            MatKhauHash = passwordService.HashPassword(request.Password),
            Role = Role.KhachVangLai,
            IsActive = true
        };
        var profile = new KhachVangLai
        {
            NguoiDung = user,
            HoTen = fullName,
            SoDienThoai = phone,
            Email = email,
            EmailNormalized = email.ToUpperInvariant()
        };
        var userId = await store.TryCreateAsync(user, profile, cancellationToken);
        return userId.HasValue
            ? ServiceResult<int>.Ok(userId.Value)
            : ServiceResult<int>.Fail("Tên đăng nhập đã tồn tại. Bạn có thể chọn tên khác hoặc đăng nhập nếu đây là tài khoản của bạn.");
    }
}
