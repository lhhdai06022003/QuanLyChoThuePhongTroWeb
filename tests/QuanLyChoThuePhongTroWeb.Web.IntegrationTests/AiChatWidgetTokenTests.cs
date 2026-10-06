using System;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuanLyChoThuePhongTroWeb.Application.Common.Enums;
using QuanLyChoThuePhongTroWeb.Application.Features.NguoiDungs.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.NguoiDungs.Services;
using QuanLyChoThuePhongTroWeb.Web.IntegrationTests.Fixtures;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Web.IntegrationTests
{
    // Widget chat lấy token antiforgery từ input[name="__RequestVerificationToken"] trên chính trang đang mở.
    // Endpoint khách thuê dùng AutoValidateAntiforgeryToken: trang nào có widget mà không có token thì chat bị 400.
    [Collection(WebTestCollection.Name)]
    public class AiChatWidgetTokenTests
    {
        private readonly CustomWebApplicationFactory _factory;

        public AiChatWidgetTokenTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
        }

        private static readonly Regex TokenInput = new(@"<input[^>]*name=""__RequestVerificationToken""", RegexOptions.IgnoreCase);

        public static TheoryData<string> TenantPages => new()
        {
            "/KhachThue/Dashboard",
            "/KhachThue/HopDong",
            "/KhachThue/HoaDon",
            "/KhachThue/LichSuThanhToan",
            "/KhachThue/ChiSoDienNuoc",
            "/KhachThue/SuCo",
            "/KhachThue/HoSo",
            "/KhachThue/ThongBao"
        };

        public static TheoryData<string> ManagerPages => new()
        {
            "/QuanLyNhaTro/Dashboard",
            "/PhongTros/QuanLyPhongTro",
            "/ChiNhanhs/QuanLyChiNhanh",
            "/QuanLyNhaTro/QuanLyDichVu",
            "/QuanLyNhaTro/ChotDienNuoc",
            "/QuanLyNhaTro/DieuKhoanMau",
            "/QuanLyNhaTro/DoiChieuThanhToan",
            "/QuanLyNhaTro/QuanLyHoaDon",
            "/QuanLyNhaTro/QuanLyHopDong",
            "/QuanLyNhaTro/LichSuThanhToan",
            "/QuanLyNhaTro/QuanLyTaiKhoanDangNhap",
            "/QuanLyNhaTro/QuanLyNguoiThue",
            "/QuanLyNhaTro/PhanCongChiNhanh",
            "/QuanLyNhaTro/ThongBao",
            "/QuanLyNhaTro/YeuCauSuCo"
        };

        private async Task<(System.Net.Http.HttpClient Client, string Name)> TenantClientAsync()
        {
            var w = await BranchScopeKit.SeedAsync(_factory);
            string name;
            using (var scope = _factory.Services.CreateScope())
            {
                var users = scope.ServiceProvider.GetRequiredService<INguoiDungService>();
                name = "kh_" + Guid.NewGuid().ToString("N")[..8];
                Assert.True((await users.AddAsync(new CreateNguoiDungReq { TenDangNhap = name, MatKhau = BranchScopeKit.Password, Role = AppRole.KhachThue, NguoiThueId = w.TenantA })).Success);
            }
            var (client, _) = await BranchScopeKit.LoginAsync(_factory, name, "/KhachThue/HoaDon");
            return (client, name);
        }

        [Theory]
        [MemberData(nameof(TenantPages))]
        public async Task TenantPage_WithChatWidget_ContainsAntiforgeryToken(string url)
        {
            var (client, _) = await TenantClientAsync();

            var res = await client.GetAsync(url);

            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            var html = await res.Content.ReadAsStringAsync();
            Assert.Contains("/KhachThue/AiAssistant/Chat", html);   // trang có widget
            Assert.True(TokenInput.IsMatch(html),
                $"LỖI SẢN PHẨM: {url} có widget chat khách thuê nhưng không có input __RequestVerificationToken, chat sẽ trả 400.");
        }

        [Theory]
        [MemberData(nameof(ManagerPages))]
        public async Task ManagerPage_WithChatWidget_ContainsAntiforgeryToken(string url)
        {
            var w = await BranchScopeKit.SeedAsync(_factory);
            var (client, _) = await BranchScopeKit.LoginAsync(_factory, w.Admin);

            var res = await client.GetAsync(url);

            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            var html = await res.Content.ReadAsStringAsync();
            Assert.Contains("/QuanLyNhaTro/AiAssistant/Chat", html);
            Assert.True(TokenInput.IsMatch(html),
                $"LỖI SẢN PHẨM: {url} có widget chat quản lý nhưng không có input __RequestVerificationToken, chat sẽ trả 400.");
        }

        [Fact]
        public async Task TenantChat_UsingTokenTakenFromDashboardPage_Succeeds_AndWithoutTokenIsRejected()
        {
            _factory.FakeAiChatModelInstance.Reset();
            var (client, _) = await TenantClientAsync();
            var html = await (await client.GetAsync("/KhachThue/Dashboard")).Content.ReadAsStringAsync();
            var token = BranchScopeKit.Token(html);
            Assert.False(string.IsNullOrEmpty(token), "Dashboard khách thuê không có token antiforgery.");

            var ok = await BranchScopeKit.SendJsonAsync(client, token, System.Net.Http.HttpMethod.Post, "/KhachThue/AiAssistant/Chat", new { message = "xin chào", history = Array.Empty<object>() });
            Assert.Equal(HttpStatusCode.OK, ok.StatusCode);

            var noToken = await client.PostAsync("/KhachThue/AiAssistant/Chat",
                new System.Net.Http.StringContent("{\"message\":\"hi\"}", System.Text.Encoding.UTF8, "application/json"));
            Assert.Equal(HttpStatusCode.BadRequest, noToken.StatusCode);
        }

        [Fact]
        public async Task Staff_WithoutAssignedBranch_GetsFixedMessage_AndModelIsNotCalled()
        {
            _factory.FakeAiChatModelInstance.Reset();
            string name;
            using (var scope = _factory.Services.CreateScope())
            {
                var users = scope.ServiceProvider.GetRequiredService<INguoiDungService>();
                name = "nv0_" + Guid.NewGuid().ToString("N")[..8];
                Assert.True((await users.AddAsync(new CreateNguoiDungReq { TenDangNhap = name, MatKhau = BranchScopeKit.Password, Role = AppRole.NhanVien })).Success);
            }
            var (client, token) = await BranchScopeKit.LoginAsync(_factory, name, "/QuanLyNhaTro/ThongBao");

            var res = await BranchScopeKit.SendJsonAsync(client, token, System.Net.Http.HttpMethod.Post, "/QuanLyNhaTro/AiAssistant/Chat", new { message = "Phòng nào còn trống?", history = Array.Empty<object>() });

            var json = await BranchScopeKit.JsonAsync(res);
            Assert.True(json.GetProperty("success").GetBoolean());
            Assert.Equal("Bạn chưa được phân công chi nhánh nào.", json.GetProperty("message").GetString());
            Assert.Empty(_factory.FakeAiChatModelInstance.Requests);
        }

        [Fact]
        public async Task Chat_MessageOver1000Chars_IsRejectedByServer_WithoutModelCall()
        {
            _factory.FakeAiChatModelInstance.Reset();
            var w = await BranchScopeKit.SeedAsync(_factory);
            var (client, token) = await BranchScopeKit.LoginAsync(_factory, w.Staff);

            var res = await BranchScopeKit.SendJsonAsync(client, token, System.Net.Http.HttpMethod.Post, "/QuanLyNhaTro/AiAssistant/Chat", new { message = new string('a', 1001), history = Array.Empty<object>() });

            var json = await BranchScopeKit.JsonAsync(res);
            Assert.False(json.GetProperty("success").GetBoolean());
            Assert.Equal("Câu hỏi tối đa 1.000 ký tự.", json.GetProperty("message").GetString());
            Assert.Empty(_factory.FakeAiChatModelInstance.Requests);
        }
    }
}
