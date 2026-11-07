using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Security;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence;
using QuanLyChoThuePhongTroWeb.Web.IntegrationTests.Fixtures;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Web.IntegrationTests
{
    // Đăng nhập có "Ghi nhớ" rồi bị ép đổi mật khẩu: cookie phát hành lại phải vẫn bền (có hạn) và mở được trang.
    [Collection(WebTestCollection.Name)]
    public class ForcedPasswordChangePersistentCookieTests
    {
        private const string ChangeUrl = "/QuanLyNhaTro/DoiMatKhau";

        private readonly CustomWebApplicationFactory _factory;

        public ForcedPasswordChangePersistentCookieTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task Staff_RememberMe_Forced_AfterChange_CookieStaysPersistentAndOpensDashboard()
        {
            var username = "ep_nho_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            const string newPassword = "Mat khau ghi nho 2026";
            await SeedAsync(username, username);

            try
            {
                var client = BranchScopeKit.NewClient(_factory);
                var page = await client.GetAsync("/QuanLyNhaTro/DangNhap");
                var login = await client.PostAsync("/QuanLyNhaTro/DangNhap", new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("__RequestVerificationToken", BranchScopeKit.Token(await page.Content.ReadAsStringAsync())),
                    new KeyValuePair<string, string>("TenDangNhap", username),
                    new KeyValuePair<string, string>("MatKhau", username),
                    new KeyValuePair<string, string>("GhiNho", "true")
                }));
                Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
                Assert.Equal(ChangeUrl, login.Headers.Location?.ToString());
                Assert.Contains(AuthCookies(login), c => c.Contains("expires=", StringComparison.OrdinalIgnoreCase));

                var token = BranchScopeKit.Token(await (await client.GetAsync(ChangeUrl)).Content.ReadAsStringAsync());
                var body = JsonSerializer.Serialize(new { MatKhauHienTai = username, MatKhauMoi = newPassword, XacNhanMatKhau = newPassword });
                var req = new HttpRequestMessage(HttpMethod.Post, ChangeUrl) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
                req.Headers.Add("RequestVerificationToken", token);
                req.Headers.Add("X-Requested-With", "XMLHttpRequest");
                var res = await client.SendAsync(req);

                Assert.Equal(HttpStatusCode.OK, res.StatusCode);
                Assert.True((await BranchScopeKit.JsonAsync(res)).GetProperty("success").GetBoolean());
                Assert.Contains(AuthCookies(res), c => c.Contains("expires=", StringComparison.OrdinalIgnoreCase));
                Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/QuanLyNhaTro/Dashboard")).StatusCode);
            }
            finally
            {
                await CleanupAsync(username);
            }
        }

        private static List<string> AuthCookies(HttpResponseMessage res) =>
            res.Headers.TryGetValues("Set-Cookie", out var values)
                ? values.Where(c => c.Contains(".AspNetCore.Cookies")).ToList()
                : new List<string>();

        private async Task SeedAsync(string username, string password)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var passwords = scope.ServiceProvider.GetRequiredService<IPasswordService>();
            db.NguoiDungs.Add(new NguoiDung
            {
                TenDangNhap = username,
                MatKhauHash = passwords.HashPassword(password),
                Role = Role.NhanVien,
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
