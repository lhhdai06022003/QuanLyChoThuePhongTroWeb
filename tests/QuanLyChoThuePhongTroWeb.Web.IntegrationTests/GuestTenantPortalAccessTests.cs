using System.Net;
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

// Khách vãng lai không vào được cổng khách thuê và không bị vòng lặp chuyển hướng
// (AccessDeniedPath trỏ về trang đăng nhập).
[Collection(WebTestCollection.Name)]
public sealed class GuestTenantPortalAccessTests(CustomWebApplicationFactory factory)
{
    private const string Password = "KhachVangLai123!";
    private const string GuestRequests = "/tai-khoan/yeu-cau-phong";

    [Fact]
    public async Task Guest_OpeningTenantPortal_IsSentToGuestRequestsWithoutLoop()
    {
        var username = $"kvl_deny_{Guid.NewGuid():N}"[..20];
        await SeedGuestAsync(username);
        try
        {
            var client = CreateClient();
            await LoginAsync(client, username, null);

            var denied = await client.GetAsync("/KhachThue/Dashboard");
            Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);
            var loginLocation = denied.Headers.Location?.ToString();
            Assert.NotNull(loginLocation);
            Assert.Contains("/QuanLyNhaTro/DangNhap", loginLocation, StringComparison.OrdinalIgnoreCase);

            var fromLogin = await client.GetAsync(loginLocation);
            Assert.Equal(HttpStatusCode.Redirect, fromLogin.StatusCode);
            Assert.Equal(GuestRequests, fromLogin.Headers.Location?.ToString());

            var page = await client.GetAsync(GuestRequests);
            Assert.Equal(HttpStatusCode.OK, page.StatusCode);
            var html = WebUtility.HtmlDecode(await page.Content.ReadAsStringAsync());
            Assert.Contains("Trang này dành cho khách thuê đã có hợp đồng", html);
        }
        finally
        {
            await CleanupUserAsync(username);
        }
    }

    [Fact]
    public async Task Guest_LoggingInWithTenantReturnUrl_IsSentToGuestRequests()
    {
        var username = $"kvl_ret_{Guid.NewGuid():N}"[..20];
        await SeedGuestAsync(username);
        try
        {
            var client = CreateClient();

            var response = await LoginAsync(client, username, "/KhachThue/HoaDon");

            Assert.Equal(GuestRequests, response.Headers.Location?.ToString());
        }
        finally
        {
            await CleanupUserAsync(username);
        }
    }

    private HttpClient CreateClient() =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

    private static async Task<HttpResponseMessage> LoginAsync(HttpClient client,
        string username, string? returnUrl)
    {
        var loginHtml = await (await client.GetAsync("/QuanLyNhaTro/DangNhap"))
            .Content.ReadAsStringAsync();
        var url = returnUrl is null ? "/QuanLyNhaTro/DangNhap"
            : "/QuanLyNhaTro/DangNhap?returnUrl=" + Uri.EscapeDataString(returnUrl);
        var response = await client.PostAsync(url, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = ExtractAntiForgeryToken(loginHtml),
            ["TenDangNhap"] = username,
            ["MatKhau"] = Password
        }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return response;
    }

    private async Task SeedGuestAsync(string username)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var passwords = scope.ServiceProvider.GetRequiredService<IPasswordService>();
        db.NguoiDungs.Add(new NguoiDung
        {
            TenDangNhap = username,
            MatKhauHash = passwords.HashPassword(Password),
            Role = Role.KhachVangLai,
            IsActive = true,
            NgayTao = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    private async Task CleanupUserAsync(string username)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.NguoiDungs.Where(u => u.TenDangNhap == username).ExecuteDeleteAsync();
    }

    private static string ExtractAntiForgeryToken(string html)
    {
        var match = Regex.Match(html, @"name=""__RequestVerificationToken""\s+type=""hidden""\s+value=""([^""]+)""");
        if (!match.Success)
        {
            match = Regex.Match(html, @"value=""([^""]+)""\s+name=""__RequestVerificationToken""");
        }

        return match.Success ? match.Groups[1].Value : string.Empty;
    }
}
