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

// Khách vãng lai không vào được cổng khách thuê (trừ /KhachThue/Dashboard, mở cho cả hai loại khách)
// và không bị vòng lặp chuyển hướng (AccessDeniedPath trỏ về trang đăng nhập).
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

            var denied = await client.GetAsync("/KhachThue/HoaDon");
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

    // URL quản lý không nằm dưới /QuanLyNhaTro: trang đăng nhập không được trả khách về lại đó.
    [Theory]
    [InlineData(Role.KhachVangLai, "/PhongTros/SoDoPhong")]
    [InlineData(Role.KhachVangLai, "/HoaDon/ExportPdf/1")]
    [InlineData(Role.KhachThue, "/PhongTros/SoDoPhong")]
    [InlineData(Role.KhachThue, "/HopDong/Print/1")]
    public async Task Customer_OpeningStaffUrlOutsideStaffArea_IsSentToDashboardWithoutLoop(
        Role role, string staffUrl)
    {
        var username = $"kh_staff_{Guid.NewGuid():N}"[..20];
        await SeedGuestAsync(username, role);
        try
        {
            var client = CreateClient();
            await LoginAsync(client, username, null);

            var denied = await client.GetAsync(staffUrl);
            Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);
            var loginLocation = denied.Headers.Location?.ToString();
            Assert.NotNull(loginLocation);
            Assert.Contains("/QuanLyNhaTro/DangNhap", loginLocation, StringComparison.OrdinalIgnoreCase);

            var fromLogin = await client.GetAsync(loginLocation);
            Assert.Equal(HttpStatusCode.Redirect, fromLogin.StatusCode);
            Assert.Equal("/KhachThue/Dashboard", fromLogin.Headers.Location?.ToString());
        }
        finally
        {
            await CleanupUserAsync(username);
        }
    }

    [Theory]
    [InlineData(Role.KhachVangLai)]
    [InlineData(Role.KhachThue)]
    public async Task Customer_LoggingInWithStaffReturnUrl_IsSentToDashboard(Role role)
    {
        var username = $"kh_rstaff_{Guid.NewGuid():N}"[..20];
        await SeedGuestAsync(username, role);
        try
        {
            var client = CreateClient();

            var response = await LoginAsync(client, username, "/PhongTros/SoDoPhong");

            Assert.Equal("/KhachThue/Dashboard", response.Headers.Location?.ToString());
        }
        finally
        {
            await CleanupUserAsync(username);
        }
    }

    [Fact]
    public async Task Guest_LoggingInWithLookalikePublicPrefix_IsNotSentThere()
    {
        var username = $"kvl_like_{Guid.NewGuid():N}"[..20];
        await SeedGuestAsync(username);
        try
        {
            var client = CreateClient();

            var response = await LoginAsync(client, username, "/phongtros/SoDoPhong");

            Assert.Equal("/KhachThue/Dashboard", response.Headers.Location?.ToString());
        }
        finally
        {
            await CleanupUserAsync(username);
        }
    }

    // Đăng nhập từ cổng xem phòng (nút Đăng nhập trên thanh đầu gửi returnUrl=/phong):
    // Admin, nhân viên vào trang quản lý; chỉ quay lại returnUrl khi đó là trang quản lý.
    [Theory]
    [InlineData(Role.Admin, "/phong")]
    [InlineData(Role.Admin, "/phong?q=quan+1")]
    [InlineData(Role.Admin, "/")]
    [InlineData(Role.Admin, "/phong/5/hen-xem")]
    [InlineData(Role.NhanVien, "/phong")]
    [InlineData(Role.NhanVien, "/tai-khoan/yeu-cau-phong")]
    [InlineData(Role.NhanVien, "/KhachThue/Dashboard")]
    [InlineData(Role.NhanVien, "/dang-ky-khach?returnUrl=%2Fphong")]
    public async Task Staff_LoggingInFromGuestPortal_GoesToManagerDashboard(Role role, string returnUrl)
    {
        var username = $"ql_pub_{Guid.NewGuid():N}"[..20];
        await SeedGuestAsync(username, role);
        try
        {
            var response = await LoginAsync(CreateClient(), username, returnUrl);

            Assert.StartsWith("/QuanLyNhaTro/Dashboard", response.Headers.Location?.ToString());
        }
        finally
        {
            await CleanupUserAsync(username);
        }
    }

    [Fact]
    public async Task Staff_LoggingInWithManagerReturnUrl_GoesBackThere()
    {
        var username = $"ql_back_{Guid.NewGuid():N}"[..20];
        await SeedGuestAsync(username, Role.Admin);
        try
        {
            var response = await LoginAsync(CreateClient(), username, "/QuanLyNhaTro/HoaDon");

            Assert.Equal("/QuanLyNhaTro/HoaDon", response.Headers.Location?.ToString());
        }
        finally
        {
            await CleanupUserAsync(username);
        }
    }

    // Khách thuê: danh sách, chi tiết phòng, trang chủ thì vào Tổng quan;
    // đang làm dở một thao tác (hẹn xem, giữ chỗ, trang tài khoản, cổng khách thuê) thì quay lại đó.
    [Theory]
    [InlineData("/phong", "/KhachThue/Dashboard")]
    [InlineData("/phong?q=quan+1", "/KhachThue/Dashboard")]
    [InlineData("/phong/5", "/KhachThue/Dashboard")]
    [InlineData("/", "/KhachThue/Dashboard")]
    [InlineData("/phong/5/hen-xem", "/phong/5/hen-xem")]
    [InlineData("/phong/5/giu-cho?source=rooms", "/phong/5/giu-cho?source=rooms")]
    [InlineData("/tai-khoan/yeu-cau-phong", "/tai-khoan/yeu-cau-phong")]
    [InlineData("/KhachThue/HoaDon", "/KhachThue/HoaDon")]
    public async Task Tenant_LoggingInFromGuestPortal_GoesToOverviewUnlessMidAction(string returnUrl, string expected)
    {
        var username = $"kt_pub_{Guid.NewGuid():N}"[..20];
        await SeedGuestAsync(username, Role.KhachThue);
        try
        {
            var client = CreateClient();
            var response = await LoginAsync(client, username, returnUrl);
            Assert.Equal(expected, response.Headers.Location?.ToString());

            // Đã đăng nhập mà mở lại trang đăng nhập với cùng returnUrl: cùng kết quả.
            var again = await client.GetAsync("/QuanLyNhaTro/DangNhap?returnUrl=" + Uri.EscapeDataString(returnUrl));
            Assert.Equal(expected, again.Headers.Location?.ToString());
        }
        finally
        {
            await CleanupUserAsync(username);
        }
    }

    [Fact]
    public async Task Guest_LoggingInFromRoomList_StaysOnGuestPortal()
    {
        var username = $"kvl_pub_{Guid.NewGuid():N}"[..20];
        await SeedGuestAsync(username);
        try
        {
            var response = await LoginAsync(CreateClient(), username, "/phong");

            Assert.Equal("/phong", response.Headers.Location?.ToString());
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

    private async Task SeedGuestAsync(string username, Role role = Role.KhachVangLai)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var passwords = scope.ServiceProvider.GetRequiredService<IPasswordService>();
        db.NguoiDungs.Add(new NguoiDung
        {
            TenDangNhap = username,
            MatKhauHash = passwords.HashPassword(Password),
            Role = role,
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
