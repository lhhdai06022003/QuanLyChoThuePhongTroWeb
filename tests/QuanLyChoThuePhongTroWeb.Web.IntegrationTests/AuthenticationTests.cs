using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using QuanLyChoThuePhongTroWeb.Application.Common.Enums;
using QuanLyChoThuePhongTroWeb.Application.Features.NguoiDungs.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.NguoiDungs.Services;
using QuanLyChoThuePhongTroWeb.Web.IntegrationTests.Fixtures;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Web.IntegrationTests
{
    [Collection(WebTestCollection.Name)]
    public class AuthenticationTests
    {
        private readonly CustomWebApplicationFactory _factory;

        public AuthenticationTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task Login_WithInvalidCredentials_ShouldFailAndNotSetAuthCookie()
        {
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

            var formContent = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("TenDangNhap", "non_existent_user"),
                new KeyValuePair<string, string>("MatKhau", "wrong_password")
            });

            var response = await client.PostAsync("/QuanLyNhaTro/DangNhap", formContent);

            // Response should remain on login page with error or fail to authenticate
            Assert.True(response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.BadRequest);

            // Check that auth cookie is not set
            var hasAuthCookie = response.Headers.TryGetValues("Set-Cookie", out var cookies) &&
                                cookies.Any(c => c.Contains(".AspNetCore.Cookies"));
            Assert.False(hasAuthCookie, "Invalid login credentials must not produce an authentication cookie.");
        }

        [Fact]
        public async Task Login_WithValidAdminCredentials_ShouldSetAuthCookie_AndAllowAccessToAdminRoute()
        {
            var username = "test_admin_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            const string password = "AdminPassword123!";

            await SeedUserAsync(username, password, AppRole.Admin);

            try
            {
                var (client, antiForgeryToken) = await CreateClientWithAntiforgeryAsync();

                var formContent = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("__RequestVerificationToken", antiForgeryToken),
                    new KeyValuePair<string, string>("TenDangNhap", username),
                    new KeyValuePair<string, string>("MatKhau", password)
                });

                var loginResponse = await client.PostAsync("/QuanLyNhaTro/DangNhap", formContent);

                // Should redirect to Admin Dashboard
                Assert.Equal(HttpStatusCode.Redirect, loginResponse.StatusCode);
                Assert.NotNull(loginResponse.Headers.Location);
                Assert.Contains("/QuanLyNhaTro", loginResponse.Headers.Location.ToString());

                // Assert Set-Cookie header contains authentication cookie
                Assert.True(
                    loginResponse.Headers.TryGetValues("Set-Cookie", out var cookies) &&
                    cookies.Any(c => c.Contains(".AspNetCore.Cookies")),
                    "Valid Admin login must produce an authentication cookie.");

                // Access protected Admin route
                var dashboardResponse = await client.GetAsync("/QuanLyNhaTro/Dashboard");
                Assert.Equal(HttpStatusCode.OK, dashboardResponse.StatusCode);
            }
            finally
            {
                await CleanupUserAsync(username);
            }
        }

        [Fact]
        public async Task Login_WithValidStaffCredentials_ShouldSetAuthCookie_AndAllowAccessToAdminRoute()
        {
            var username = "test_staff_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            const string password = "StaffPassword123!";

            await SeedUserAsync(username, password, AppRole.NhanVien);

            try
            {
                var (client, antiForgeryToken) = await CreateClientWithAntiforgeryAsync();

                var formContent = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("__RequestVerificationToken", antiForgeryToken),
                    new KeyValuePair<string, string>("TenDangNhap", username),
                    new KeyValuePair<string, string>("MatKhau", password)
                });

                var loginResponse = await client.PostAsync("/QuanLyNhaTro/DangNhap", formContent);

                Assert.Equal(HttpStatusCode.Redirect, loginResponse.StatusCode);
                Assert.NotNull(loginResponse.Headers.Location);
                Assert.Contains("/QuanLyNhaTro", loginResponse.Headers.Location.ToString());

                // Access protected Admin route (allowed for Admin,NhanVien)
                var dashboardResponse = await client.GetAsync("/QuanLyNhaTro/Dashboard");
                Assert.Equal(HttpStatusCode.OK, dashboardResponse.StatusCode);
            }
            finally
            {
                await CleanupUserAsync(username);
            }
        }

        [Fact]
        public async Task Login_WithValidTenantCredentials_ShouldSetAuthCookie_AndAllowAccessToTenantRoute()
        {
            var username = "test_tenant_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            const string password = "TenantPassword123!";

            await SeedUserAsync(username, password, AppRole.KhachThue);

            try
            {
                var (client, antiForgeryToken) = await CreateClientWithAntiforgeryAsync();

                var formContent = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("__RequestVerificationToken", antiForgeryToken),
                    new KeyValuePair<string, string>("TenDangNhap", username),
                    new KeyValuePair<string, string>("MatKhau", password)
                });

                var loginResponse = await client.PostAsync("/QuanLyNhaTro/DangNhap", formContent);

                Assert.Equal(HttpStatusCode.Redirect, loginResponse.StatusCode);
                Assert.NotNull(loginResponse.Headers.Location);
                Assert.Contains("/KhachThue", loginResponse.Headers.Location.ToString());

                // Access protected Tenant route
                var tenantResponse = await client.GetAsync("/KhachThue/LichSuThanhToan");
                Assert.Equal(HttpStatusCode.OK, tenantResponse.StatusCode);
            }
            finally
            {
                await CleanupUserAsync(username);
            }
        }

        [Fact]
        public async Task TenantUser_AccessingAdminRoute_ShouldBeRedirectedToLoginOrDenied()
        {
            var username = "test_tenant_denied_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            const string password = "TenantPassword123!";

            await SeedUserAsync(username, password, AppRole.KhachThue);

            try
            {
                var (client, antiForgeryToken) = await CreateClientWithAntiforgeryAsync();

                var formContent = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("__RequestVerificationToken", antiForgeryToken),
                    new KeyValuePair<string, string>("TenDangNhap", username),
                    new KeyValuePair<string, string>("MatKhau", password)
                });

                var loginResponse = await client.PostAsync("/QuanLyNhaTro/DangNhap", formContent);
                Assert.Equal(HttpStatusCode.Redirect, loginResponse.StatusCode);

                // Tenant attempts to access Admin-only route
                var adminRouteResponse = await client.GetAsync("/QuanLyNhaTro/Dashboard");

                // Cookie authentication defaults to redirecting to AccessDeniedPath (/QuanLyNhaTro/DangNhap) or returning Forbidden (403)
                Assert.True(
                    adminRouteResponse.StatusCode == HttpStatusCode.Redirect ||
                    adminRouteResponse.StatusCode == HttpStatusCode.Forbidden,
                    "Tenant user must not be granted access to Admin dashboard.");

                if (adminRouteResponse.StatusCode == HttpStatusCode.Redirect)
                {
                    Assert.NotNull(adminRouteResponse.Headers.Location);
                    Assert.Contains("DangNhap", adminRouteResponse.Headers.Location.ToString());
                }
            }
            finally
            {
                await CleanupUserAsync(username);
            }
        }

        [Fact]
        public async Task Logout_WithAntiforgeryToken_ShouldSignOut_AndSubsequentAccessRedirectsToLogin()
        {
            var username = "test_logout_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            const string password = "LogoutPassword123!";

            await SeedUserAsync(username, password, AppRole.Admin);

            try
            {
                var (client, antiForgeryToken) = await CreateClientWithAntiforgeryAsync();

                // 2. Perform valid login
                var formContent = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("__RequestVerificationToken", antiForgeryToken),
                    new KeyValuePair<string, string>("TenDangNhap", username),
                    new KeyValuePair<string, string>("MatKhau", password)
                });

                var loginResponse = await client.PostAsync("/QuanLyNhaTro/DangNhap", formContent);
                Assert.Equal(HttpStatusCode.Redirect, loginResponse.StatusCode);

                // 3. Verify access to protected route works while authenticated
                var accessBeforeLogout = await client.GetAsync("/QuanLyNhaTro/Dashboard");
                Assert.Equal(HttpStatusCode.OK, accessBeforeLogout.StatusCode);

                // Get fresh antiforgery token for logout POST
                var dashboardHtml = await accessBeforeLogout.Content.ReadAsStringAsync();
                var logoutAntiforgery = ExtractAntiForgeryToken(dashboardHtml);
                if (string.IsNullOrWhiteSpace(logoutAntiforgery))
                {
                    logoutAntiforgery = antiForgeryToken;
                }

                // 4. Perform Logout via POST /QuanLyNhaTro/DangXuat
                var logoutContent = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("__RequestVerificationToken", logoutAntiforgery)
                });

                var logoutResponse = await client.PostAsync("/QuanLyNhaTro/DangXuat", logoutContent);

                // Logout redirects to DangNhap
                Assert.Equal(HttpStatusCode.Redirect, logoutResponse.StatusCode);
                Assert.NotNull(logoutResponse.Headers.Location);
                Assert.Contains("DangNhap", logoutResponse.Headers.Location.ToString());

                // 5. Verify subsequent access to protected route redirects back to login
                var accessAfterLogout = await client.GetAsync("/QuanLyNhaTro/Dashboard");
                Assert.Equal(HttpStatusCode.Redirect, accessAfterLogout.StatusCode);
                Assert.NotNull(accessAfterLogout.Headers.Location);
                Assert.Contains("DangNhap", accessAfterLogout.Headers.Location.ToString());
            }
            finally
            {
                await CleanupUserAsync(username);
            }
        }

        private async Task SeedUserAsync(string username, string password, AppRole role)
        {
            using var scope = _factory.Services.CreateScope();
            var nguoiDungService = scope.ServiceProvider.GetRequiredService<INguoiDungService>();

            var result = await nguoiDungService.AddAsync(new CreateNguoiDungReq
            {
                TenDangNhap = username,
                MatKhau = password,
                Role = role
            });

            if (!result.Success)
            {
                throw new InvalidOperationException($"Failed to seed test user '{username}': {result.Message}");
            }
        }

        private async Task CleanupUserAsync(string username)
        {
            try
            {
                using var scope = _factory.Services.CreateScope();
                var nguoiDungService = scope.ServiceProvider.GetRequiredService<INguoiDungService>();
                var allUsers = await nguoiDungService.GetAllAsync();
                var user = allUsers.FirstOrDefault(u => u.TenDangNhap == username);
                if (user != null)
                {
                    await nguoiDungService.DeleteAsync(user.NguoiDungId);
                }
            }
            catch
            {
                // Best-effort cleanup
            }
        }

        private async Task<(HttpClient client, string antiForgeryToken)> CreateClientWithAntiforgeryAsync()
        {
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = true
            });

            var getResponse = await client.GetAsync("/QuanLyNhaTro/DangNhap");
            var html = await getResponse.Content.ReadAsStringAsync();
            var token = ExtractAntiForgeryToken(html);
            return (client, token);
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
