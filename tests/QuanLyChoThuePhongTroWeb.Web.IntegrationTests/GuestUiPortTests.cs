using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using QuanLyChoThuePhongTroWeb.Application.Features.PublicRooms;

namespace QuanLyChoThuePhongTroWeb.Web.IntegrationTests;

// UI port checks use a room stub and disable initialization: no database is opened.
public sealed class GuestUiPortTests
{
    [Theory]
    [InlineData("/phong", "discovery-heading")]
    [InlineData("/phong/10", "guest-detail")]
    [InlineData("/phong/cam-nang", "guest-site")]
    [InlineData("/phong/da-luu", "guest-site")]
    [InlineData("/phong/so-sanh?ids=10", "guest-site")]
    [InlineData("/dang-ky-khach", "guest-account-form")]
    public async Task PublicPagesRenderWithoutLogin(string path, string marker)
    {
        await using var factory = new GuestUiFactory();
        using var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        var result = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        var html = await result.Content.ReadAsStringAsync();
        Assert.Contains(marker, html);
        Assert.DoesNotContain("id=\"loginForm\"", html);
    }

    [Theory]
    [InlineData("/phong/10/hen-xem")]
    [InlineData("/phong/10/giu-cho?source=rooms")]
    public async Task RoomActionsOnlyOpenExistingLoginWhenExplicitlyRequested(string path)
    {
        await using var factory = new GuestUiFactory();
        using var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        var prompt = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, prompt.StatusCode);
        var html = await prompt.Content.ReadAsStringAsync();
        Assert.Contains("data-login-continue", html);
        Assert.DoesNotContain("id=\"loginForm\"", html);
        var login = await client.GetStringAsync("/QuanLyNhaTro/DangNhap?returnUrl=" + WebUtility.UrlEncode(path));
        Assert.Contains("id=\"loginForm\"", login);
        Assert.Contains("id=\"txtTenDangNhap\"", login);
    }

    [Fact]
    public async Task AnonymousHomeOpensRoomsAndSelectionUsesPublicData()
    {
        await using var factory = new GuestUiFactory();
        using var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        var home = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.Redirect, home.StatusCode);
        Assert.Equal("/phong", home.Headers.Location?.ToString());
        var data = await client.GetStringAsync("/phong/du-lieu?ids=10,999");
        Assert.Contains("\"id\":10", data);
        Assert.Contains("\"unavailableIds\":[999]", data);
    }

    private sealed class GuestUiFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("Database:InitializeOnStartup", "false");
            builder.UseSetting("BackgroundJobs:Enabled", "false");
            builder.UseSetting("ConnectionStrings:DefaultConnection",
                "Host=localhost;Database=unused_ui_test;Username=unused;Password=unused");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IPublicRoomService>();
                services.AddSingleton<IPublicRoomService, RoomStub>();
            });
        }
    }

    private sealed class RoomStub : IPublicRoomService
    {
        private static readonly PublicRoomDto Room = new(10, "103", "Thủ Đức", "Võ Văn Ngân",
            3800000, 28, 3, 1, "Phòng công khai dùng cho kiểm thử giao diện");
        public Task<IReadOnlyList<PublicRoomDto>> ListAsync(IReadOnlyCollection<int>? ids,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PublicRoomDto>>([Room]);
        public Task<PublicRoomDto?> GetAsync(int id, IReadOnlyCollection<int>? ids,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(id == Room.PhongTroId ? Room : null);
    }
}
