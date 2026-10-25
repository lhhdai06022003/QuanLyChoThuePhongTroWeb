using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Security;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence;
using QuanLyChoThuePhongTroWeb.Web.IntegrationTests.Fixtures;

namespace QuanLyChoThuePhongTroWeb.Web.IntegrationTests;

// Màn Tài khoản: tài khoản khách vãng lai sửa được (giữ nguyên quyền) nhưng không tạo mới
// hay chuyển tài khoản khác sang quyền này, vì khách vãng lai cần hồ sơ do khách tự đăng ký.
[Collection(WebTestCollection.Name)]
public sealed class AccountGuestRoleTests(CustomWebApplicationFactory factory)
{
    private const string Password = "AccountRole123!";

    [Fact]
    public async Task Admin_CannotCreateGuestAccount()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var adminId = await SeedUserAsync("acc_admin_" + suffix, Role.Admin);
        var newUsername = "acc_new_" + suffix;
        try
        {
            var (client, token) = await LoginAsync("acc_admin_" + suffix);

            var result = await PostJsonAsync(client, token, "/QuanLyNhaTro/NguoiDung/CreateApi",
                new { tenDangNhap = newUsername, matKhau = Password, role = 3 });

            Assert.False(result.GetProperty("success").GetBoolean());
            Assert.Contains("khách tự đăng ký", result.GetProperty("message").GetString());
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.False(await db.NguoiDungs.AnyAsync(u => u.TenDangNhap == newUsername));
        }
        finally
        {
            await CleanupAsync(adminId);
        }
    }

    [Fact]
    public async Task Admin_CanEditGuestAccount_ButNotSwitchStaffToGuest()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var adminId = await SeedUserAsync("acc_admin_" + suffix, Role.Admin);
        var guestId = await SeedUserAsync("acc_guest_" + suffix, Role.KhachVangLai);
        var staffId = await SeedUserAsync("acc_staff_" + suffix, Role.NhanVien);
        try
        {
            var (client, token) = await LoginAsync("acc_admin_" + suffix);

            var lockGuest = await PostJsonAsync(client, token, $"/QuanLyNhaTro/NguoiDung/EditApi/{guestId}",
                new { nguoiDungId = guestId, role = 3, isActive = false });
            var staffToGuest = await PostJsonAsync(client, token, $"/QuanLyNhaTro/NguoiDung/EditApi/{staffId}",
                new { nguoiDungId = staffId, role = 3, isActive = true });

            Assert.True(lockGuest.GetProperty("success").GetBoolean());
            Assert.False(staffToGuest.GetProperty("success").GetBoolean());
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var guest = await db.NguoiDungs.AsNoTracking().SingleAsync(u => u.NguoiDungId == guestId);
            Assert.Equal(Role.KhachVangLai, guest.Role);
            Assert.False(guest.IsActive);
            Assert.Equal(Role.NhanVien,
                (await db.NguoiDungs.AsNoTracking().SingleAsync(u => u.NguoiDungId == staffId)).Role);
        }
        finally
        {
            await CleanupAsync(adminId, guestId, staffId);
        }
    }

    private async Task<int> SeedUserAsync(string username, Role role)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var passwords = scope.ServiceProvider.GetRequiredService<IPasswordService>();
        var user = new NguoiDung
        {
            TenDangNhap = username,
            MatKhauHash = passwords.HashPassword(Password),
            Role = role,
            IsActive = true,
            NgayTao = DateTime.UtcNow
        };
        db.NguoiDungs.Add(user);
        await db.SaveChangesAsync();
        return user.NguoiDungId;
    }

    private async Task CleanupAsync(params int[] userIds)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.NguoiDungs.Where(u => userIds.Contains(u.NguoiDungId)).ExecuteDeleteAsync();
    }

    private async Task<(HttpClient Client, string Token)> LoginAsync(string username)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        var loginHtml = await client.GetStringAsync("/QuanLyNhaTro/DangNhap");
        var response = await client.PostAsync("/QuanLyNhaTro/DangNhap", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = Token(loginHtml), ["TenDangNhap"] = username, ["MatKhau"] = Password
        }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return (client, Token(await client.GetStringAsync("/QuanLyNhaTro/QuanLyTaiKhoanDangNhap")));
    }

    private static async Task<JsonElement> PostJsonAsync(HttpClient client, string token, string url, object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };
        request.Headers.Add("RequestVerificationToken", token);
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();
    }

    private static string Token(string html)
    {
        var match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(match.Success);
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }
}
