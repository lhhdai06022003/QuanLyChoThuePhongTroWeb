using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence;
using QuanLyChoThuePhongTroWeb.Web.IntegrationTests.Fixtures;

namespace QuanLyChoThuePhongTroWeb.Web.IntegrationTests;

[Collection(WebTestCollection.Name)]
public sealed class GuestPortalFlowTests(CustomWebApplicationFactory factory)
{
    [Fact]
    public async Task GuestCanRegisterLoginRequestViewingAndHold()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var username = "guest_http_" + suffix;
        var email = "guest_http_" + suffix + "@example.com";
        int? branchId = null, roomId = null;
        try
        {
            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var branch = new ChiNhanh
                {
                    MaChiNhanh = "W" + suffix[..7], TenChiNhanh = "Test " + suffix,
                    DiaChi = "A", MoTa = "A", SoDienThoai = "0900000001"
                };
                db.ChiNhanhs.Add(branch);
                await db.SaveChangesAsync();
                var room = new PhongTro
                {
                    ChiNhanhId = branch.ChiNhanhId, SoPhong = "W" + suffix,
                    GiaThue = 1000000m, DienTich = 20, MoTa = "Test",
                    TrangThai = TrangThaiPhong.Trong, DuocDangTin = true
                };
                db.PhongTros.Add(room);
                await db.SaveChangesAsync();
                branchId = branch.ChiNhanhId;
                roomId = room.PhongTroId;
            }

            var client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false, HandleCookies = true
            });
            Assert.Equal(HttpStatusCode.OK,
                (await client.GetAsync($"/phong/{roomId}" )).StatusCode);

            var registerHtml = await (await client.GetAsync("/dang-ky-khach"))
                .Content.ReadAsStringAsync();
            var registerResponse = await client.PostAsync("/dang-ky-khach",
                Form(("__RequestVerificationToken", Token(registerHtml)),
                    ("Username", username), ("Password", "GuestTest123!"),
                    ("FullName", "Khách kiểm thử"), ("Phone", "0912345678"),
                    ("Email", email)));
            Assert.Equal(HttpStatusCode.Redirect, registerResponse.StatusCode);

            var loginHtml = await (await client.GetAsync("/QuanLyNhaTro/DangNhap"))
                .Content.ReadAsStringAsync();
            var loginResponse = await client.PostAsync("/QuanLyNhaTro/DangNhap",
                Form(("__RequestVerificationToken", Token(loginHtml)),
                    ("TenDangNhap", username), ("MatKhau", "GuestTest123!")));
            Assert.Equal(HttpStatusCode.Redirect, loginResponse.StatusCode);
            Assert.Equal("/tai-khoan/yeu-cau-phong",
                loginResponse.Headers.Location?.ToString());

            var viewingPage = await client.GetAsync($"/phong/{roomId}/hen-xem");
            Assert.Equal(HttpStatusCode.OK, viewingPage.StatusCode);
            var preferredLocal = DateTimeOffset.UtcNow.AddDays(2)
                .ToOffset(TimeSpan.FromHours(7)).ToString("yyyy-MM-ddTHH:mm");
            var viewingResponse = await client.PostAsync($"/phong/{roomId}/hen-xem",
                Form(("__RequestVerificationToken",
                        Token(await viewingPage.Content.ReadAsStringAsync())),
                    ("PreferredTimeLocal", preferredLocal),
                    ("FullName", "Khách kiểm thử"), ("Phone", "0912345678"),
                    ("Email", email)));
            Assert.Equal(HttpStatusCode.Redirect, viewingResponse.StatusCode);
            Assert.Equal("/tai-khoan/yeu-cau-phong",
                viewingResponse.Headers.Location?.ToString());

            var holdPage = await client.GetAsync($"/phong/{roomId}/giu-cho");
            Assert.Equal(HttpStatusCode.OK, holdPage.StatusCode);
            var holdResponse = await client.PostAsync($"/phong/{roomId}/giu-cho",
                Form(("__RequestVerificationToken",
                    Token(await holdPage.Content.ReadAsStringAsync()))));
            Assert.Equal(HttpStatusCode.Redirect, holdResponse.StatusCode);
            Assert.Equal("/tai-khoan/yeu-cau-phong",
                holdResponse.Headers.Location?.ToString());

            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                Assert.Single(await db.YeuCauXemPhongs.Where(v =>
                    v.KhachVangLai!.NguoiDung.TenDangNhap == username).ToListAsync());
                Assert.Single(await db.YeuCauGiuChos.Where(h =>
                    h.KhachVangLai.NguoiDung.TenDangNhap == username).ToListAsync());
            }

            var mineResponse = await client.GetAsync("/tai-khoan/yeu-cau-phong");
            Assert.Equal(HttpStatusCode.OK, mineResponse.StatusCode);
            var mineHtml = WebUtility.HtmlDecode(
                await mineResponse.Content.ReadAsStringAsync());
            Assert.Contains("Chờ nhân viên xác nhận", mineHtml);
            Assert.Contains("Chờ nhân viên duyệt", mineHtml);
        }
        finally
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var guest = await db.KhachVangLais.AsNoTracking()
                .Where(g => g.NguoiDung.TenDangNhap == username)
                .Select(g => new { g.KhachVangLaiId, g.NguoiDungId })
                .SingleOrDefaultAsync();
            if (guest is not null)
            {
                var viewingIds = await db.YeuCauXemPhongs.Where(v =>
                    v.KhachVangLaiId == guest.KhachVangLaiId)
                    .Select(v => v.YeuCauXemPhongId).ToArrayAsync();
                var holdIds = await db.YeuCauGiuChos.Where(h =>
                    h.KhachVangLaiId == guest.KhachVangLaiId)
                    .Select(h => h.YeuCauGiuChoId).ToArrayAsync();
                await db.LichSuTrangThaiYeuCauGiuChos.Where(h =>
                    holdIds.Contains(h.YeuCauGiuChoId)).ExecuteDeleteAsync();
                await db.YeuCauGiuChos.Where(h =>
                    holdIds.Contains(h.YeuCauGiuChoId)).ExecuteDeleteAsync();
                await db.LichSuTrangThaiYeuCauXemPhongs.Where(h =>
                    viewingIds.Contains(h.YeuCauXemPhongId)).ExecuteDeleteAsync();
                await db.YeuCauXemPhongs.Where(v =>
                    viewingIds.Contains(v.YeuCauXemPhongId)).ExecuteDeleteAsync();
                await db.KhachVangLais.Where(g =>
                    g.KhachVangLaiId == guest.KhachVangLaiId).ExecuteDeleteAsync();
                await db.NguoiDungs.Where(u =>
                    u.NguoiDungId == guest.NguoiDungId).ExecuteDeleteAsync();
            }
            if (roomId.HasValue)
                await db.PhongTros.Where(r => r.PhongTroId == roomId.Value)
                    .ExecuteDeleteAsync();
            if (branchId.HasValue)
                await db.ChiNhanhs.Where(b => b.ChiNhanhId == branchId.Value)
                    .ExecuteDeleteAsync();
        }
    }

    private static FormUrlEncodedContent Form(params (string Key, string Value)[] fields) =>
        new(fields.Select(field =>
            new KeyValuePair<string, string>(field.Key, field.Value)));

    private static string Token(string html)
    {
        var match = Regex.Match(html,
            "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        if (!match.Success)
            match = Regex.Match(html,
                "value=\"([^\"]+)\"[^>]*name=\"__RequestVerificationToken\"");
        return match.Success ? match.Groups[1].Value :
            throw new InvalidOperationException("Antiforgery token missing.");
    }
}
