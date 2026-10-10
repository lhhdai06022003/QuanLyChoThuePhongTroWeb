using System;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuanLyChoThuePhongTroWeb.Application.Common.Enums;
using QuanLyChoThuePhongTroWeb.Application.Features.NguoiDungs.Services;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence;
using QuanLyChoThuePhongTroWeb.Web.IntegrationTests.Fixtures;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Web.IntegrationTests
{
    [Collection(WebTestCollection.Name)]
    public class SessionRevalidationTests
    {
        private readonly CustomWebApplicationFactory _factory;

        public SessionRevalidationTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public void AppRole_Defines_KhachVangLai()
        {
            // Q1 (ĐỔI 2026-10-09, người dùng xác nhận khi gộp cổng khách vãng lai): AppRole thêm
            // KhachVangLai = 3 để tài khoản khách vãng lai giữ được phiên đăng nhập. Quyết định cũ
            // ngày 2026-09-28 (không mở rộng AppRole) không còn hiệu lực.
            var names = Enum.GetNames<AppRole>();
            Assert.Contains("KhachVangLai", names, StringComparer.Ordinal);
            Assert.Equal(new[] { "Admin", "NhanVien", "KhachThue", "KhachVangLai" }, names);
        }

        [Fact]
        public async Task KhachVangLaiAccount_LogsIn_AndCanOpenGuestRequests()
        {
            var username = $"khvangl_{Guid.NewGuid():N}".Substring(0, 20);
            const string password = "KhachVangLai123!";

            using (var seedScope = _factory.Services.CreateScope())
            {
                var db = seedScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var passwordService = seedScope.ServiceProvider.GetRequiredService<QuanLyChoThuePhongTroWeb.Application.Abstractions.Security.IPasswordService>();

                db.NguoiDungs.Add(new QuanLyChoThuePhongTroWeb.Domain.Entities.NguoiDung
                {
                    TenDangNhap = username,
                    MatKhauHash = passwordService.HashPassword(password),
                    Role = QuanLyChoThuePhongTroWeb.Domain.Enums.Role.KhachVangLai,
                    IsActive = true,
                    NgayTao = DateTime.UtcNow
                });
                await db.SaveChangesAsync();
            }

            try
            {
                var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
                {
                    AllowAutoRedirect = false,
                    HandleCookies = true
                });

                var getLoginResponse = await client.GetAsync("/QuanLyNhaTro/DangNhap");
                var loginHtml = await getLoginResponse.Content.ReadAsStringAsync();
                var token = ExtractAntiForgeryToken(loginHtml);

                var loginContent = new System.Net.Http.FormUrlEncodedContent(new[]
                {
                    new System.Collections.Generic.KeyValuePair<string, string>("__RequestVerificationToken", token),
                    new System.Collections.Generic.KeyValuePair<string, string>("TenDangNhap", username),
                    new System.Collections.Generic.KeyValuePair<string, string>("MatKhau", password)
                });

                var loginResponse = await client.PostAsync("/QuanLyNhaTro/DangNhap", loginContent);

                Assert.Equal(HttpStatusCode.Redirect, loginResponse.StatusCode);
                Assert.True(
                    loginResponse.Headers.TryGetValues("Set-Cookie", out var cookies) &&
                    cookies.Any(c => c.Contains(".AspNetCore.Cookies")),
                    "Đăng nhập tài khoản khách vãng lai phải tạo cookie xác thực.");
                Assert.Equal("/tai-khoan/yeu-cau-phong", loginResponse.Headers.Location?.ToString());

                var afterLoginResponse = await client.GetAsync("/tai-khoan/yeu-cau-phong");
                Assert.Equal(HttpStatusCode.OK, afterLoginResponse.StatusCode);
            }
            finally
            {
                await CleanupUserAsync(username);
            }
        }

        [Fact]
        public async Task StaffSession_LockedAfterLogin_NextRequest_IsTreatedAsSignedOut()
        {
            var username = $"sess_lock_{Guid.NewGuid():N}".Substring(0, 20);
            const string password = "SessionLock123!";

            using var scope = _factory.Services.CreateScope();
            var nguoiDungService = scope.ServiceProvider.GetRequiredService<INguoiDungService>();
            var createResult = await nguoiDungService.AddAsync(new QuanLyChoThuePhongTroWeb.Application.Features.NguoiDungs.DTOs.CreateNguoiDungReq
            {
                TenDangNhap = username,
                MatKhau = password,
                Role = AppRole.NhanVien
            });
            Assert.True(createResult.Success);

            try
            {
                var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
                {
                    AllowAutoRedirect = false,
                    HandleCookies = true
                });

                var getLoginResponse = await client.GetAsync("/QuanLyNhaTro/DangNhap");
                var loginHtml = await getLoginResponse.Content.ReadAsStringAsync();
                var token = ExtractAntiForgeryToken(loginHtml);

                var loginContent = new System.Net.Http.FormUrlEncodedContent(new[]
                {
                    new System.Collections.Generic.KeyValuePair<string, string>("__RequestVerificationToken", token),
                    new System.Collections.Generic.KeyValuePair<string, string>("TenDangNhap", username),
                    new System.Collections.Generic.KeyValuePair<string, string>("MatKhau", password)
                });

                var loginResponse = await client.PostAsync("/QuanLyNhaTro/DangNhap", loginContent);
                Assert.Equal(HttpStatusCode.Redirect, loginResponse.StatusCode);

                // Phiên còn hợp lệ: truy cập Dashboard được (Admin,NhanVien)
                var beforeLockResponse = await client.GetAsync("/QuanLyNhaTro/Dashboard");
                Assert.Equal(HttpStatusCode.OK, beforeLockResponse.StatusCode);

                // Admin khóa tài khoản trực tiếp trong DB test (không qua cookie cũ)
                using (var lockScope = _factory.Services.CreateScope())
                {
                    var db = lockScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    var user = await db.NguoiDungs.FirstAsync(u => u.TenDangNhap == username);
                    user.IsActive = false;
                    await db.SaveChangesAsync();
                }

                // Request kế tiếp bằng cookie cũ: OnValidatePrincipal phát hiện IsActive=false -> SignOut -> 302 DangNhap
                var afterLockResponse = await client.GetAsync("/QuanLyNhaTro/Dashboard");
                Assert.Equal(HttpStatusCode.Redirect, afterLockResponse.StatusCode);
                Assert.NotNull(afterLockResponse.Headers.Location);
                Assert.Contains("/QuanLyNhaTro/DangNhap", afterLockResponse.Headers.Location!.ToString(), StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                await CleanupUserAsync(username);
            }
        }

        [Fact]
        public async Task StaffSession_RoleChangedAfterLogin_NextRequest_IsTreatedAsSignedOut()
        {
            var username = $"sess_role_{Guid.NewGuid():N}".Substring(0, 20);
            const string password = "SessionRole123!";

            using var scope = _factory.Services.CreateScope();
            var nguoiDungService = scope.ServiceProvider.GetRequiredService<INguoiDungService>();
            var createResult = await nguoiDungService.AddAsync(new QuanLyChoThuePhongTroWeb.Application.Features.NguoiDungs.DTOs.CreateNguoiDungReq
            {
                TenDangNhap = username,
                MatKhau = password,
                Role = AppRole.NhanVien
            });
            Assert.True(createResult.Success);

            try
            {
                var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
                {
                    AllowAutoRedirect = false,
                    HandleCookies = true
                });

                var getLoginResponse = await client.GetAsync("/QuanLyNhaTro/DangNhap");
                var loginHtml = await getLoginResponse.Content.ReadAsStringAsync();
                var token = ExtractAntiForgeryToken(loginHtml);

                var loginContent = new System.Net.Http.FormUrlEncodedContent(new[]
                {
                    new System.Collections.Generic.KeyValuePair<string, string>("__RequestVerificationToken", token),
                    new System.Collections.Generic.KeyValuePair<string, string>("TenDangNhap", username),
                    new System.Collections.Generic.KeyValuePair<string, string>("MatKhau", password)
                });

                var loginResponse = await client.PostAsync("/QuanLyNhaTro/DangNhap", loginContent);
                Assert.Equal(HttpStatusCode.Redirect, loginResponse.StatusCode);

                var beforeChangeResponse = await client.GetAsync("/QuanLyNhaTro/Dashboard");
                Assert.Equal(HttpStatusCode.OK, beforeChangeResponse.StatusCode);

                // Đổi role trực tiếp trong DB test: cookie cũ vẫn mang claim NhanVien -> lệch role thật
                using (var roleScope = _factory.Services.CreateScope())
                {
                    var db = roleScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    var user = await db.NguoiDungs.FirstAsync(u => u.TenDangNhap == username);
                    user.Role = QuanLyChoThuePhongTroWeb.Domain.Enums.Role.KhachThue;
                    await db.SaveChangesAsync();
                }

                var afterChangeResponse = await client.GetAsync("/QuanLyNhaTro/Dashboard");
                Assert.Equal(HttpStatusCode.Redirect, afterChangeResponse.StatusCode);
                Assert.NotNull(afterChangeResponse.Headers.Location);
                Assert.Contains("/QuanLyNhaTro/DangNhap", afterChangeResponse.Headers.Location!.ToString(), StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                await CleanupUserAsync(username);
            }
        }

        private async Task CleanupUserAsync(string username)
        {
            try
            {
                using var scope = _factory.Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var user = await db.NguoiDungs.FirstOrDefaultAsync(u => u.TenDangNhap == username);
                if (user != null)
                {
                    db.NguoiDungs.Remove(user);
                    await db.SaveChangesAsync();
                }
            }
            catch
            {
                // Best-effort cleanup
            }
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
}
