using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
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
    // Hub thông báo chỉ nhận kết nối đã đăng nhập (spec bảo mật xác thực §2.7).
    [Collection(WebTestCollection.Name)]
    public class SignalRTests
    {
        private const string NegotiateUrl = "/thongBaoHub/negotiate?negotiateVersion=1";

        private readonly CustomWebApplicationFactory _factory;

        public SignalRTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
        }

        private static HttpRequestMessage NegotiateRequest()
        {
            // Client SignalR JS gửi kèm X-Requested-With: cookie auth trả 401 thay vì chuyển hướng về trang đăng nhập.
            var request = new HttpRequestMessage(HttpMethod.Post, NegotiateUrl) { Content = new StringContent(string.Empty) };
            request.Headers.Add("X-Requested-With", "XMLHttpRequest");
            return request;
        }

        [Fact]
        public async Task SignalR_ThongBaoHub_Negotiate_Anonymous_ReturnsUnauthorized()
        {
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

            var response = await client.SendAsync(NegotiateRequest());

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            var content = await response.Content.ReadAsStringAsync();
            Assert.DoesNotContain("connectionId", content);
        }

        [Fact]
        public async Task SignalR_ThongBaoHub_Negotiate_AnonymousWithoutAjaxHeader_IsNotAccepted()
        {
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

            var response = await client.PostAsync(NegotiateUrl, new StringContent(string.Empty));

            Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.DoesNotContain("connectionId", await response.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task AnonymousPageOnManagerLayout_DoesNotLoadHubScripts()
        {
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

            var response = await client.GetAsync("/Home/Privacy");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var html = await response.Content.ReadAsStringAsync();
            Assert.DoesNotContain("/js/thongbao.js", html);
            Assert.DoesNotContain("signalr.min.js", html);
        }

        [Fact]
        public async Task SignalR_ThongBaoHub_Negotiate_SignedIn_ReturnsSuccess()
        {
            var username = "hub_staff_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            const string password = "StaffPassword123!";

            using (var scope = _factory.Services.CreateScope())
            {
                var users = scope.ServiceProvider.GetRequiredService<INguoiDungService>();
                var created = await users.AddAsync(new CreateNguoiDungReq { TenDangNhap = username, MatKhau = password, Role = AppRole.NhanVien });
                Assert.True(created.Success, created.Message);
            }

            int? userId = null;
            try
            {
                var client = BranchScopeKit.NewClient(_factory);
                var loginPage = await client.GetAsync("/QuanLyNhaTro/DangNhap");
                var login = await client.PostAsync("/QuanLyNhaTro/DangNhap", new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("__RequestVerificationToken", BranchScopeKit.Token(await loginPage.Content.ReadAsStringAsync())),
                    new KeyValuePair<string, string>("TenDangNhap", username),
                    new KeyValuePair<string, string>("MatKhau", password)
                }));
                Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);

                var response = await client.SendAsync(NegotiateRequest());

                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                var content = await response.Content.ReadAsStringAsync();
                Assert.Contains("connectionId", content);
                Assert.Contains("availableTransports", content);
            }
            finally
            {
                using var scope = _factory.Services.CreateScope();
                var users = scope.ServiceProvider.GetRequiredService<INguoiDungService>();
                foreach (var u in await users.GetAllAsync())
                {
                    if (u.TenDangNhap == username) userId = u.NguoiDungId;
                }
                if (userId.HasValue) await users.DeleteAsync(userId.Value);
            }
        }
    }
}
