using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Security;
using QuanLyChoThuePhongTroWeb.Application.Common.Enums;
using QuanLyChoThuePhongTroWeb.Application.Features.NguoiDungs.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.NguoiDungs.Services;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence;
using QuanLyChoThuePhongTroWeb.Web.IntegrationTests.Fixtures;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Web.IntegrationTests
{
    // Màn đổi mật khẩu cổng quản lý /QuanLyNhaTro/DoiMatKhau (spec bảo mật xác thực §2.5).
    [Collection(WebTestCollection.Name)]
    public class ManagerChangePasswordTests
    {
        private const string Url = "/QuanLyNhaTro/DoiMatKhau";
        private const string Password = "StaffPassword123!";

        private readonly CustomWebApplicationFactory _factory;

        public ManagerChangePasswordTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task Anonymous_IsRedirectedToLogin()
        {
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

            var res = await client.GetAsync(Url);

            Assert.Equal(HttpStatusCode.Redirect, res.StatusCode);
            Assert.Contains("DangNhap", res.Headers.Location?.ToString());
        }

        [Fact]
        public async Task Get_ShowsFormWithThreeFields()
        {
            var username = NewName("dmk_get");
            await CreateAsync(username, AppRole.NhanVien);

            try
            {
                var (client, _) = await LoginAsync(username, Password);

                var res = await client.GetAsync(Url);

                Assert.Equal(HttpStatusCode.OK, res.StatusCode);
                var html = await res.Content.ReadAsStringAsync();
                Assert.Contains("id=\"dmkMatKhauHienTai\"", html);
                Assert.Contains("id=\"dmkMatKhauMoi\"", html);
                Assert.Contains("id=\"dmkXacNhan\"", html);
                Assert.Contains("/js/doi-mat-khau.js", html);
            }
            finally
            {
                await CleanupAsync(username);
            }
        }

        [Fact]
        public async Task Post_WrongCurrentPassword_Fails()
        {
            var username = NewName("dmk_sai");
            await CreateAsync(username, AppRole.NhanVien);

            try
            {
                var (client, token) = await LoginAsync(username, Password);

                var res = await PostAsync(client, token, "sai-mat-khau", "Mat khau moi 2026", "Mat khau moi 2026");

                Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
                var json = await BranchScopeKit.JsonAsync(res);
                Assert.False(json.GetProperty("success").GetBoolean());
            }
            finally
            {
                await CleanupAsync(username);
            }
        }

        [Theory]
        [InlineData("ngan")]
        [InlineData("12345678")]
        [InlineData(Password)]
        public async Task Post_PolicyViolation_Fails(string newPassword)
        {
            var username = NewName("dmk_cs");
            await CreateAsync(username, AppRole.NhanVien);

            try
            {
                var (client, token) = await LoginAsync(username, Password);

                var res = await PostAsync(client, token, Password, newPassword, newPassword);

                Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
                Assert.False((await BranchScopeKit.JsonAsync(res)).GetProperty("success").GetBoolean());
            }
            finally
            {
                await CleanupAsync(username);
            }
        }

        [Fact]
        public async Task Post_PasswordEqualToUsername_Fails()
        {
            var username = NewName("dmk_ten");
            await CreateAsync(username, AppRole.NhanVien);

            try
            {
                var (client, token) = await LoginAsync(username, Password);

                var res = await PostAsync(client, token, Password, username, username);

                Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
            }
            finally
            {
                await CleanupAsync(username);
            }
        }

        [Fact]
        public async Task Post_ConfirmationMismatch_Fails()
        {
            var username = NewName("dmk_xn");
            await CreateAsync(username, AppRole.NhanVien);

            try
            {
                var (client, token) = await LoginAsync(username, Password);

                var res = await PostAsync(client, token, Password, "Mat khau moi 2026", "Mat khau khac 2026");

                Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
                Assert.False((await BranchScopeKit.JsonAsync(res)).GetProperty("success").GetBoolean());
            }
            finally
            {
                await CleanupAsync(username);
            }
        }

        [Fact]
        public async Task Post_Valid_ChangesPassword_NewWorks_OldRejected()
        {
            var username = NewName("dmk_ok");
            const string newPassword = "Mat khau moi 2026";
            await CreateAsync(username, AppRole.NhanVien);

            try
            {
                var (client, token) = await LoginAsync(username, Password);

                var res = await PostAsync(client, token, Password, newPassword, newPassword);

                Assert.Equal(HttpStatusCode.OK, res.StatusCode);
                var json = await BranchScopeKit.JsonAsync(res);
                Assert.True(json.GetProperty("success").GetBoolean(), json.GetProperty("message").GetString());
                Assert.Equal("/QuanLyNhaTro/Dashboard", json.GetProperty("redirectUrl").GetString());

                var oldLogin = await ForcedPasswordChangeTests.LoginAsync(BranchScopeKit.NewClient(_factory), username, Password);
                Assert.Equal(HttpStatusCode.OK, oldLogin.StatusCode);

                var newLogin = await ForcedPasswordChangeTests.LoginAsync(BranchScopeKit.NewClient(_factory), username, newPassword);
                Assert.Equal(HttpStatusCode.Redirect, newLogin.StatusCode);
                Assert.StartsWith("/QuanLyNhaTro/Dashboard", newLogin.Headers.Location?.ToString());
            }
            finally
            {
                await CleanupAsync(username);
            }
        }

        [Fact]
        public async Task ForcedUser_AfterChange_ReissuedCookieOpensDashboard()
        {
            var username = NewName("dmk_ep");
            await SeedWithPasswordAsync(username, username, Role.NhanVien);
            const string newPassword = "Mat khau moi 2026";

            try
            {
                var client = BranchScopeKit.NewClient(_factory);
                var login = await ForcedPasswordChangeTests.LoginAsync(client, username, username);
                Assert.Equal(Url, login.Headers.Location?.ToString());
                var token = BranchScopeKit.Token(await (await client.GetAsync(Url)).Content.ReadAsStringAsync());
                Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/QuanLyNhaTro/Dashboard")).StatusCode);

                var res = await PostAsync(client, token, username, newPassword, newPassword);

                Assert.Equal(HttpStatusCode.OK, res.StatusCode);
                Assert.True((await BranchScopeKit.JsonAsync(res)).GetProperty("success").GetBoolean());
                Assert.True(res.Headers.TryGetValues("Set-Cookie", out var cookies) && cookies.Any(c => c.Contains(".AspNetCore.Cookies")));

                Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/QuanLyNhaTro/Dashboard")).StatusCode);
            }
            finally
            {
                await CleanupAsync(username);
            }
        }

        [Fact]
        public async Task Header_HasChangePasswordLink_ForAdmin()
        {
            var username = NewName("dmk_ad");
            await CreateAsync(username, AppRole.Admin);

            try
            {
                var (client, _) = await LoginAsync(username, Password);

                var html = WebUtility.HtmlDecode(await (await client.GetAsync("/QuanLyNhaTro/Dashboard")).Content.ReadAsStringAsync());

                Assert.Contains("href=\"/QuanLyNhaTro/DoiMatKhau\"", html);
                Assert.Contains("Đổi mật khẩu", html);
            }
            finally
            {
                await CleanupAsync(username);
            }
        }

        private static string NewName(string prefix) => prefix + "_" + Guid.NewGuid().ToString("N").Substring(0, 8);

        private static Task<HttpResponseMessage> PostAsync(HttpClient client, string token, string current, string newPassword, string confirm)
        {
            var body = JsonSerializer.Serialize(new { MatKhauHienTai = current, MatKhauMoi = newPassword, XacNhanMatKhau = confirm });
            var req = new HttpRequestMessage(HttpMethod.Post, Url) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
            req.Headers.Add("RequestVerificationToken", token);
            req.Headers.Add("X-Requested-With", "XMLHttpRequest");
            return client.SendAsync(req);
        }

        private async Task<(HttpClient Client, string Token)> LoginAsync(string username, string password)
        {
            var client = BranchScopeKit.NewClient(_factory);
            var login = await ForcedPasswordChangeTests.LoginAsync(client, username, password);
            Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
            var page = await client.GetAsync(Url);
            Assert.Equal(HttpStatusCode.OK, page.StatusCode);
            var token = BranchScopeKit.Token(await page.Content.ReadAsStringAsync());
            Assert.False(string.IsNullOrEmpty(token));
            return (client, token);
        }

        private async Task CreateAsync(string username, AppRole role)
        {
            using var scope = _factory.Services.CreateScope();
            var users = scope.ServiceProvider.GetRequiredService<INguoiDungService>();
            var result = await users.AddAsync(new CreateNguoiDungReq { TenDangNhap = username, MatKhau = Password, Role = role });
            Assert.True(result.Success, result.Message);
        }

        private async Task SeedWithPasswordAsync(string username, string password, Role role)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var passwords = scope.ServiceProvider.GetRequiredService<IPasswordService>();
            db.NguoiDungs.Add(new NguoiDung
            {
                TenDangNhap = username,
                MatKhauHash = passwords.HashPassword(password),
                Role = role,
                IsActive = true,
                NgayTao = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        private async Task CleanupAsync(string username)
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
    }
}
