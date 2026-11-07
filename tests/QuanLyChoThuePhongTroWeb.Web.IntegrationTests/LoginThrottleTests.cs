using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using QuanLyChoThuePhongTroWeb.Application.Common.Enums;
using QuanLyChoThuePhongTroWeb.Application.Features.NguoiDungs.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.NguoiDungs.Services;
using QuanLyChoThuePhongTroWeb.Web.IntegrationTests.Fixtures;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Web.IntegrationTests
{
    // Khóa đăng nhập tạm thời sau 5 lần sai (spec bảo mật xác thực §2.6). Bộ đếm là singleton sống suốt bộ test:
    // mỗi test dùng tên đăng nhập ngẫu nhiên riêng. TestServer không có IP nên chỉ bộ đếm theo tên chạy.
    [Collection(WebTestCollection.Name)]
    public class LoginThrottleTests
    {
        private const string LockMessage = "Bạn đã nhập sai quá nhiều lần";

        private readonly CustomWebApplicationFactory _factory;

        public LoginThrottleTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task FiveWrongPasswords_ThenCorrectPassword_IsStillRejected_WithoutCookie()
        {
            var username = "khoa_" + Guid.NewGuid().ToString("N").Substring(0, 10);
            const string password = "StaffPassword123!";
            var userId = await CreateStaffAsync(username, password);

            try
            {
                var client = BranchScopeKit.NewClient(_factory);
                for (var i = 0; i < 5; i++)
                {
                    var wrong = await ForcedPasswordChangeTests.LoginAsync(client, username, "sai-mat-khau-" + i);
                    Assert.Equal(HttpStatusCode.OK, wrong.StatusCode);
                    Assert.False(HasAuthCookie(wrong));
                }

                var locked = await ForcedPasswordChangeTests.LoginAsync(client, username, password);

                Assert.Equal(HttpStatusCode.OK, locked.StatusCode);
                Assert.False(HasAuthCookie(locked), "Đang khóa thì mật khẩu đúng cũng không được cấp cookie.");
                var html = WebUtility.HtmlDecode(await locked.Content.ReadAsStringAsync());
                Assert.Contains(LockMessage, html);
                Assert.Contains("15 phút", html);
            }
            finally
            {
                await DeleteAsync(userId);
            }
        }

        [Fact]
        public async Task FourWrongPasswords_ThenCorrectPassword_LogsIn()
        {
            var username = "chua_khoa_" + Guid.NewGuid().ToString("N").Substring(0, 10);
            const string password = "StaffPassword123!";
            var userId = await CreateStaffAsync(username, password);

            try
            {
                var client = BranchScopeKit.NewClient(_factory);
                for (var i = 0; i < 4; i++)
                {
                    await ForcedPasswordChangeTests.LoginAsync(client, username, "sai-mat-khau-" + i);
                }

                var ok = await ForcedPasswordChangeTests.LoginAsync(client, username, password);

                Assert.Equal(HttpStatusCode.Redirect, ok.StatusCode);
                Assert.True(HasAuthCookie(ok));
            }
            finally
            {
                await DeleteAsync(userId);
            }
        }

        [Fact]
        public async Task UnknownUsername_IsLockedToo_WithSameMessage()
        {
            var username = "khong_co_" + Guid.NewGuid().ToString("N").Substring(0, 10);
            var client = BranchScopeKit.NewClient(_factory);

            for (var i = 0; i < 5; i++)
            {
                var wrong = WebUtility.HtmlDecode(await (await ForcedPasswordChangeTests.LoginAsync(client, username, "sai")).Content.ReadAsStringAsync());
                Assert.Contains("Tên đăng nhập hoặc mật khẩu không chính xác", wrong);
            }

            var locked = WebUtility.HtmlDecode(await (await ForcedPasswordChangeTests.LoginAsync(client, username, "sai")).Content.ReadAsStringAsync());
            Assert.Contains(LockMessage, locked);
        }

        private static bool HasAuthCookie(HttpResponseMessage res) =>
            res.Headers.TryGetValues("Set-Cookie", out var cookies) && cookies.Any(c => c.Contains(".AspNetCore.Cookies"));

        private async Task<int> CreateStaffAsync(string username, string password)
        {
            using var scope = _factory.Services.CreateScope();
            var users = scope.ServiceProvider.GetRequiredService<INguoiDungService>();
            var result = await users.AddAsync(new CreateNguoiDungReq { TenDangNhap = username, MatKhau = password, Role = AppRole.NhanVien });
            Assert.True(result.Success, result.Message);
            return (await users.GetAllAsync()).Single(u => u.TenDangNhap == username).NguoiDungId;
        }

        private async Task DeleteAsync(int userId)
        {
            using var scope = _factory.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<INguoiDungService>().DeleteAsync(userId);
        }
    }
}
