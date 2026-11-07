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
    // Ép đổi mật khẩu mặc định (spec bảo mật xác thực §2.2, §2.5). Tài khoản có mật khẩu yếu được ghi thẳng vào DB
    // vì AddAsync đã từ chối mật khẩu đó theo chính sách mới.
    [Collection(WebTestCollection.Name)]
    public class ForcedPasswordChangeTests
    {
        private const string ManagerChangeUrl = "/QuanLyNhaTro/DoiMatKhau";
        private const string TenantChangeUrl = "/KhachThue/HoSo?doiMatKhau=1";

        private readonly CustomWebApplicationFactory _factory;

        public ForcedPasswordChangeTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task Staff_WithPasswordEqualToUsername_IsRedirectedToChangePassword_OnEveryManagerPage()
        {
            var username = "ep_nv_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            await SeedUserAsync(username, username, Role.NhanVien);

            try
            {
                var client = BranchScopeKit.NewClient(_factory);
                var login = await LoginAsync(client, username, username);
                Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
                Assert.Equal(ManagerChangeUrl, login.Headers.Location?.ToString());

                var dashboard = await client.GetAsync("/QuanLyNhaTro/Dashboard");
                Assert.Equal(HttpStatusCode.Redirect, dashboard.StatusCode);
                Assert.Equal(ManagerChangeUrl, dashboard.Headers.Location?.ToString());

                // Controller quản lý không kế thừa AdminBaseController cũng bị chặn.
                var thongBao = await client.GetAsync("/QuanLyNhaTro/ThongBao");
                Assert.Equal(HttpStatusCode.Redirect, thongBao.StatusCode);
                Assert.Equal(ManagerChangeUrl, thongBao.Headers.Location?.ToString());

                var changePage = await client.GetAsync(ManagerChangeUrl);
                Assert.Equal(HttpStatusCode.OK, changePage.StatusCode);
                var html = WebUtility.HtmlDecode(await changePage.Content.ReadAsStringAsync());
                Assert.Contains("Mật khẩu hiện tại là mật khẩu mặc định", html);
                // API việc cần làm trả 403 khi đang bị ép đổi: trang không được nạp badge gọi API đó.
                Assert.DoesNotContain("viec-can-lam-badge.js", html);
            }
            finally
            {
                await CleanupAsync(username);
            }
        }

        [Fact]
        public async Task Staff_Forced_AjaxRequests_Get403Json_WithRedirectUrl()
        {
            var username = "ep_ajax_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            await SeedUserAsync(username, username, Role.NhanVien);

            try
            {
                var client = BranchScopeKit.NewClient(_factory);
                Assert.Equal(HttpStatusCode.Redirect, (await LoginAsync(client, username, username)).StatusCode);
                var token = BranchScopeKit.Token(await (await client.GetAsync(ManagerChangeUrl)).Content.ReadAsStringAsync());

                var post = new HttpRequestMessage(HttpMethod.Post, "/DienNuoc/SaveChotDienNuoc")
                {
                    Content = new StringContent("{}", Encoding.UTF8, "application/json")
                };
                post.Headers.Add("X-Requested-With", "XMLHttpRequest");
                post.Headers.Add("RequestVerificationToken", token);
                var postRes = await client.SendAsync(post);
                await AssertRequireChangeJsonAsync(postRes, ManagerChangeUrl);

                var get = new HttpRequestMessage(HttpMethod.Get, "/QuanLyNhaTro/Dashboard");
                get.Headers.Add("Accept", "application/json");
                await AssertRequireChangeJsonAsync(await client.SendAsync(get), ManagerChangeUrl);
            }
            finally
            {
                await CleanupAsync(username);
            }
        }

        [Fact]
        public async Task Staff_Forced_CanStillLogOut()
        {
            var username = "ep_out_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            await SeedUserAsync(username, username, Role.NhanVien);

            try
            {
                var client = BranchScopeKit.NewClient(_factory);
                Assert.Equal(HttpStatusCode.Redirect, (await LoginAsync(client, username, username)).StatusCode);
                var token = BranchScopeKit.Token(await (await client.GetAsync(ManagerChangeUrl)).Content.ReadAsStringAsync());

                var logout = await client.PostAsync("/QuanLyNhaTro/DangXuat", new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("__RequestVerificationToken", token)
                }));

                Assert.Equal(HttpStatusCode.Redirect, logout.StatusCode);
                Assert.Contains("DangNhap", logout.Headers.Location?.ToString());

                var after = await client.GetAsync("/QuanLyNhaTro/Dashboard");
                Assert.Equal(HttpStatusCode.Redirect, after.StatusCode);
                Assert.Contains("DangNhap", after.Headers.Location?.ToString());
            }
            finally
            {
                await CleanupAsync(username);
            }
        }

        [Fact]
        public async Task Staff_Forced_ReturnUrlIsIgnored()
        {
            var username = "ep_ret_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            await SeedUserAsync(username, username, Role.NhanVien);

            try
            {
                var client = BranchScopeKit.NewClient(_factory);
                var login = await LoginAsync(client, username, username, "/QuanLyNhaTro/HoaDon");

                Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
                Assert.Equal(ManagerChangeUrl, login.Headers.Location?.ToString());
            }
            finally
            {
                await CleanupAsync(username);
            }
        }

        [Fact]
        public async Task Staff_WithOwnPassword_IsNotForced()
        {
            var username = "ep_ok_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            const string password = "StaffPassword123!";
            await SeedUserAsync(username, password, Role.NhanVien);

            try
            {
                var client = BranchScopeKit.NewClient(_factory);
                var login = await LoginAsync(client, username, password);
                Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
                Assert.StartsWith("/QuanLyNhaTro/Dashboard", login.Headers.Location?.ToString());

                Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/QuanLyNhaTro/Dashboard")).StatusCode);
                var changePage = WebUtility.HtmlDecode(await (await client.GetAsync(ManagerChangeUrl)).Content.ReadAsStringAsync());
                Assert.DoesNotContain("Mật khẩu hiện tại là mật khẩu mặc định", changePage);
            }
            finally
            {
                await CleanupAsync(username);
            }
        }

        [Fact]
        public async Task Tenant_WithPhonePassword_IsRedirectedToProfile_AndProfileStillOpens()
        {
            var (username, phone) = await SeedTenantWithPhonePasswordAsync("ep_kt");

            try
            {
                var client = BranchScopeKit.NewClient(_factory);
                var login = await LoginAsync(client, username, phone);
                Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
                Assert.Equal(TenantChangeUrl, login.Headers.Location?.ToString());

                var hoaDon = await client.GetAsync("/KhachThue/HoaDon");
                Assert.Equal(HttpStatusCode.Redirect, hoaDon.StatusCode);
                Assert.Equal(TenantChangeUrl, hoaDon.Headers.Location?.ToString());

                var dashboard = await client.GetAsync("/KhachThue/Dashboard");
                Assert.Equal(HttpStatusCode.Redirect, dashboard.StatusCode);
                Assert.Equal(TenantChangeUrl, dashboard.Headers.Location?.ToString());

                var profile = await client.GetAsync(TenantChangeUrl);
                Assert.Equal(HttpStatusCode.OK, profile.StatusCode);
                var html = WebUtility.HtmlDecode(await profile.Content.ReadAsStringAsync());
                Assert.Contains("Mật khẩu hiện tại là mật khẩu mặc định", html);
                Assert.Contains("cardDoiMatKhau", html);
            }
            finally
            {
                await CleanupAsync(username);
            }
        }

        // Luồng thật (spec §2.3, D3): khách đã tự đặt mật khẩu, Admin bấm "đặt lại về số điện thoại" qua API của màn quản lý
        // tài khoản, khách đăng nhập bằng số điện thoại thì bị ép đổi; mật khẩu cũ không còn dùng được.
        [Fact]
        public async Task Tenant_AfterAdminResetToPhone_LoginWithPhone_IsForced()
        {
            const string ownPassword = "TenantOwnPass123!";
            var (username, phone) = await SeedTenantWithPhonePasswordAsync("ep_reset", ownPassword);
            var adminName = $"admin_reset_{Guid.NewGuid().ToString("N").Substring(0, 8)}";
            int tenantUserId;
            using (var scope = _factory.Services.CreateScope())
            {
                var users = scope.ServiceProvider.GetRequiredService<QuanLyChoThuePhongTroWeb.Application.Features.NguoiDungs.Services.INguoiDungService>();
                var created = await users.AddAsync(new QuanLyChoThuePhongTroWeb.Application.Features.NguoiDungs.DTOs.CreateNguoiDungReq
                {
                    TenDangNhap = adminName,
                    MatKhau = BranchScopeKit.Password,
                    Role = QuanLyChoThuePhongTroWeb.Application.Common.Enums.AppRole.Admin
                });
                Assert.True(created.Success, created.Message);
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                tenantUserId = (await db.NguoiDungs.FirstAsync(u => u.TenDangNhap == username)).NguoiDungId;
            }

            try
            {
                // Trước khi đặt lại: mật khẩu riêng của khách không bị ép.
                var before = BranchScopeKit.NewClient(_factory);
                var loginOwn = await LoginAsync(before, username, ownPassword);
                Assert.Equal(HttpStatusCode.Redirect, loginOwn.StatusCode);
                Assert.NotEqual(TenantChangeUrl, loginOwn.Headers.Location?.ToString());

                var (adminClient, token) = await BranchScopeKit.LoginAsync(_factory, adminName, "/QuanLyNhaTro/QuanLyTaiKhoanDangNhap");
                var reset = await BranchScopeKit.PostFormAsync(adminClient, token,
                    $"/QuanLyNhaTro/NguoiDung/ResetPasswordToPhoneApi/{tenantUserId}", new Dictionary<string, string>());
                Assert.Equal(HttpStatusCode.OK, reset.StatusCode);
                Assert.True((await BranchScopeKit.JsonAsync(reset)).GetProperty("success").GetBoolean());

                var oldClient = BranchScopeKit.NewClient(_factory);
                var loginOld = await LoginAsync(oldClient, username, ownPassword);
                Assert.Equal(HttpStatusCode.OK, loginOld.StatusCode);

                var client = BranchScopeKit.NewClient(_factory);
                var login = await LoginAsync(client, username, phone);
                Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
                Assert.Equal(TenantChangeUrl, login.Headers.Location?.ToString());

                var hoaDon = await client.GetAsync("/KhachThue/HoaDon");
                Assert.Equal(HttpStatusCode.Redirect, hoaDon.StatusCode);
                Assert.Equal(TenantChangeUrl, hoaDon.Headers.Location?.ToString());
            }
            finally
            {
                await CleanupAsync(username);
                await CleanupAsync(adminName);
            }
        }

        [Fact]
        public async Task Tenant_Forced_NewPasswordEqualToPhone_IsRejected()
        {
            var (username, phone) = await SeedTenantWithPhonePasswordAsync("ep_ktsdt");

            try
            {
                var client = BranchScopeKit.NewClient(_factory);
                Assert.Equal(HttpStatusCode.Redirect, (await LoginAsync(client, username, phone)).StatusCode);
                var token = BranchScopeKit.Token(await (await client.GetAsync(TenantChangeUrl)).Content.ReadAsStringAsync());

                var res = await PostTenantChangeAsync(client, token, phone, "+84" + phone.Substring(1));

                Assert.Equal(HttpStatusCode.OK, res.StatusCode);
                var json = await BranchScopeKit.JsonAsync(res);
                Assert.False(json.GetProperty("success").GetBoolean());

                // Vẫn bị ép đổi.
                Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/KhachThue/HoaDon")).StatusCode);
            }
            finally
            {
                await CleanupAsync(username);
            }
        }

        [Fact]
        public async Task Tenant_Forced_ChangesPassword_ThenReissuedCookieOpensTenantPages()
        {
            var (username, phone) = await SeedTenantWithPhonePasswordAsync("ep_ktdoi");
            const string newPassword = "Khach thue moi 2026";

            try
            {
                var client = BranchScopeKit.NewClient(_factory);
                Assert.Equal(HttpStatusCode.Redirect, (await LoginAsync(client, username, phone)).StatusCode);
                var token = BranchScopeKit.Token(await (await client.GetAsync(TenantChangeUrl)).Content.ReadAsStringAsync());

                var res = await PostTenantChangeAsync(client, token, phone, newPassword);

                Assert.Equal(HttpStatusCode.OK, res.StatusCode);
                var json = await BranchScopeKit.JsonAsync(res);
                Assert.True(json.GetProperty("success").GetBoolean(), json.GetProperty("message").GetString());
                Assert.Equal("/KhachThue/Dashboard", json.GetProperty("redirectUrl").GetString());
                Assert.True(res.Headers.TryGetValues("Set-Cookie", out var cookies) && cookies.Any(c => c.Contains(".AspNetCore.Cookies")),
                    "Đổi mật khẩu xong phải phát hành lại cookie không còn claim ép đổi.");

                Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/KhachThue/HoaDon")).StatusCode);

                // Đăng nhập lại bằng mật khẩu mới thì không bị ép nữa.
                var fresh = BranchScopeKit.NewClient(_factory);
                var login = await LoginAsync(fresh, username, newPassword);
                Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
                Assert.Equal("/KhachThue/Dashboard", login.Headers.Location?.ToString());
            }
            finally
            {
                await CleanupAsync(username);
            }
        }

        [Fact]
        public async Task Tenant_WithOwnPassword_ChangePasswordResponse_HasNoRedirect()
        {
            var (username, _) = await SeedTenantWithPhonePasswordAsync("ep_ktok", "TenantPassword123!");

            try
            {
                var client = BranchScopeKit.NewClient(_factory);
                var login = await LoginAsync(client, username, "TenantPassword123!");
                Assert.Equal("/KhachThue/Dashboard", login.Headers.Location?.ToString());
                var token = BranchScopeKit.Token(await (await client.GetAsync("/KhachThue/HoSo")).Content.ReadAsStringAsync());

                var res = await PostTenantChangeAsync(client, token, "TenantPassword123!", "Khach thue moi 2026");

                var json = await BranchScopeKit.JsonAsync(res);
                Assert.True(json.GetProperty("success").GetBoolean(), json.GetProperty("message").GetString());
                Assert.False(json.TryGetProperty("redirectUrl", out _));
            }
            finally
            {
                await CleanupAsync(username);
            }
        }

        private static async Task AssertRequireChangeJsonAsync(HttpResponseMessage res, string expectedUrl)
        {
            Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
            Assert.False(doc.RootElement.GetProperty("success").GetBoolean());
            Assert.True(doc.RootElement.GetProperty("requireChangePassword").GetBoolean());
            Assert.Equal(expectedUrl, doc.RootElement.GetProperty("redirectUrl").GetString());
        }

        private static Task<HttpResponseMessage> PostTenantChangeAsync(HttpClient client, string token, string oldPassword, string newPassword)
        {
            var body = JsonSerializer.Serialize(new { MatKhauCu = oldPassword, MatKhauMoi = newPassword, XacNhanMatKhau = newPassword });
            var req = new HttpRequestMessage(HttpMethod.Post, "/KhachThue/HoSo/DoiMatKhau")
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
            req.Headers.Add("RequestVerificationToken", token);
            req.Headers.Add("X-Requested-With", "XMLHttpRequest");
            return client.SendAsync(req);
        }

        internal static async Task<HttpResponseMessage> LoginAsync(HttpClient client, string username, string password, string? returnUrl = null)
        {
            var page = await client.GetAsync("/QuanLyNhaTro/DangNhap");
            var url = "/QuanLyNhaTro/DangNhap" + (returnUrl == null ? string.Empty : "?returnUrl=" + Uri.EscapeDataString(returnUrl));
            return await client.PostAsync(url, new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("__RequestVerificationToken", BranchScopeKit.Token(await page.Content.ReadAsStringAsync())),
                new KeyValuePair<string, string>("TenDangNhap", username),
                new KeyValuePair<string, string>("MatKhau", password)
            }));
        }

        private async Task SeedUserAsync(string username, string password, Role role)
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

        // Khách thuê có hồ sơ người thuê; mặc định mật khẩu = số điện thoại như tài khoản tạo khi lập hợp đồng.
        private async Task<(string Username, string Phone)> SeedTenantWithPhonePasswordAsync(string prefix, string? password = null)
        {
            var suffix = Guid.NewGuid().ToString("N").Substring(0, 8);
            var username = $"{prefix}_{suffix}@test.com";
            var phone = "09" + Random.Shared.Next(10000000, 99999999);

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var passwords = scope.ServiceProvider.GetRequiredService<IPasswordService>();
            var tenant = new NguoiThue
            {
                HoVaTen = "Khach ep doi " + suffix,
                Email = username,
                SoDienThoai = phone,
                CCCD = "079" + suffix
            };
            db.NguoiThues.Add(tenant);
            await db.SaveChangesAsync();

            db.NguoiDungs.Add(new NguoiDung
            {
                TenDangNhap = username,
                MatKhauHash = passwords.HashPassword(password ?? phone),
                Role = Role.KhachThue,
                IsActive = true,
                NguoiThueId = tenant.NguoiThueId,
                NgayTao = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
            return (username, phone);
        }

        private async Task CleanupAsync(string username)
        {
            try
            {
                using var scope = _factory.Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var user = await db.NguoiDungs.FirstOrDefaultAsync(u => u.TenDangNhap == username);
                if (user == null) return;

                var tenantId = user.NguoiThueId;
                db.NguoiDungs.Remove(user);
                await db.SaveChangesAsync();

                if (tenantId.HasValue)
                {
                    var tenant = await db.NguoiThues.FirstOrDefaultAsync(t => t.NguoiThueId == tenantId.Value);
                    if (tenant != null)
                    {
                        db.NguoiThues.Remove(tenant);
                        await db.SaveChangesAsync();
                    }
                }
            }
            catch
            {
                // Best-effort cleanup
            }
        }
    }
}
