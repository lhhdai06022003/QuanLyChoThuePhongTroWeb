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
        if (request is null ||
            string.IsNullOrWhiteSpace(request.Username) ||
            !Regex.IsMatch(request.Username.Trim(), "^[A-Za-z0-9._-]{3,50}$") ||
            string.IsNullOrWhiteSpace(request.Password) || request.Password.Length is < 8 or > 128 ||
            string.IsNullOrWhiteSpace(request.FullName) || request.FullName.Trim().Length > 100 ||
            string.IsNullOrWhiteSpace(request.Phone) ||
            !Regex.IsMatch(request.Phone.Trim(), "^\\+?[0-9]{9,15}$") ||
            string.IsNullOrWhiteSpace(request.Email) || request.Email.Trim().Length > 150 ||
            !new EmailAddressAttribute().IsValid(request.Email.Trim()))
            return ServiceResult<int>.Fail("Thông tin đăng ký không hợp lệ.");

        var user = new NguoiDung
        {
            TenDangNhap = request.Username.Trim(),
            MatKhauHash = passwordService.HashPassword(request.Password),
            Role = Role.KhachVangLai,
            IsActive = true
        };
        var profile = new KhachVangLai
        {
            NguoiDung = user,
            HoTen = request.FullName.Trim(),
            SoDienThoai = request.Phone.Trim(),
            Email = request.Email.Trim(),
            EmailNormalized = request.Email.Trim().ToUpperInvariant()
        };
        var userId = await store.TryCreateAsync(user, profile, cancellationToken);
        return userId.HasValue
            ? ServiceResult<int>.Ok(userId.Value)
            : ServiceResult<int>.Fail("Tên đăng nhập đã tồn tại.");
    }
}
