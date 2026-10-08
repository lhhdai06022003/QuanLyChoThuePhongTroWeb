using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuanLyChoThuePhongTroWeb.Application.Features.Reservations;
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
        int? branchId = null, roomId = null, adminId = null;
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
                var admin = new NguoiDung
                {
                    TenDangNhap = "staff_http_" + suffix,
                    MatKhauHash = "test", Role = Role.Admin
                };
                db.NguoiDungs.Add(admin);
                await db.SaveChangesAsync();
                adminId = admin.NguoiDungId;
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
            var listingHtml = await (await client.GetAsync($"/phong?khuVuc={suffix}"))
                .Content.ReadAsStringAsync();
            Assert.Contains($"/phong/{roomId}\"", listingHtml);
            Assert.Contains($"/phong/{roomId}/hen-xem", listingHtml);
            Assert.Contains($"/phong/{roomId}/giu-cho", listingHtml);

            var viewingPath = $"/phong/{roomId}/hen-xem";
            var anonymousResponse = await client.GetAsync(viewingPath);
            Assert.Equal(HttpStatusCode.Redirect, anonymousResponse.StatusCode);
            Assert.Contains(viewingPath,
                WebUtility.UrlDecode(anonymousResponse.Headers.Location!.ToString()));

            var registerHtml = await (await client.GetAsync("/dang-ky-khach"))
                .Content.ReadAsStringAsync();
            var registerResponse = await client.PostAsync("/dang-ky-khach",
                Form(("__RequestVerificationToken", Token(registerHtml)),
                    ("Username", username), ("Password", "GuestTest123!"),
                    ("FullName", "Khách kiểm thử"), ("Phone", "0912345678"),
                    ("Email", email), ("ReturnUrl", viewingPath)));
            Assert.Equal(HttpStatusCode.Redirect, registerResponse.StatusCode);

            var loginHtml = await (await client.GetAsync(registerResponse.Headers.Location))
                .Content.ReadAsStringAsync();
            var loginResponse = await client.PostAsync("/QuanLyNhaTro/DangNhap",
                Form(("__RequestVerificationToken", Token(loginHtml)),
                    ("TenDangNhap", username), ("MatKhau", "GuestTest123!"),
                    ("returnUrl", Hidden(loginHtml, "returnUrl"))));
            Assert.Equal(HttpStatusCode.Redirect, loginResponse.StatusCode);
            Assert.Equal(viewingPath,
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

            var requestsHtml = await (await client.GetAsync("/tai-khoan/yeu-cau-phong"))
                .Content.ReadAsStringAsync();
            Assert.Contains("Hủy lịch xem", WebUtility.HtmlDecode(requestsHtml));
            int viewingId;
            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                viewingId = await db.YeuCauXemPhongs.Where(v =>
                    v.KhachVangLai!.NguoiDung.TenDangNhap == username)
                    .Select(v => v.YeuCauXemPhongId).SingleAsync();
            }
            var cancelResponse = await client.PostAsync(
                $"/tai-khoan/yeu-cau-phong/lich-xem/{viewingId}/huy",
                Form(("__RequestVerificationToken", Token(requestsHtml))));
            Assert.Equal(HttpStatusCode.Redirect, cancelResponse.StatusCode);
            requestsHtml = await (await client.GetAsync("/tai-khoan/yeu-cau-phong"))
                .Content.ReadAsStringAsync();
            Assert.Contains("Đã hủy", WebUtility.HtmlDecode(requestsHtml));
            var logoutResponse = await client.PostAsync("/QuanLyNhaTro/DangXuat",
                Form(("__RequestVerificationToken", Token(requestsHtml))));
            Assert.Equal(HttpStatusCode.Redirect, logoutResponse.StatusCode);

            var holdPath = $"/phong/{roomId}/giu-cho?source=rooms";
            anonymousResponse = await client.GetAsync(holdPath);
            Assert.Equal(HttpStatusCode.Redirect, anonymousResponse.StatusCode);
            loginHtml = await (await client.GetAsync(anonymousResponse.Headers.Location))
                .Content.ReadAsStringAsync();
            loginResponse = await client.PostAsync("/QuanLyNhaTro/DangNhap",
                Form(("__RequestVerificationToken", Token(loginHtml)),
                    ("TenDangNhap", username), ("MatKhau", "GuestTest123!"),
                    ("returnUrl", Hidden(loginHtml, "returnUrl"))));
            Assert.Equal(holdPath, loginResponse.Headers.Location?.ToString());

            var holdPage = await client.GetAsync($"/phong/{roomId}/giu-cho");
            Assert.Equal(HttpStatusCode.OK, holdPage.StatusCode);
            var holdResponse = await client.PostAsync($"/phong/{roomId}/giu-cho",
                Form(("__RequestVerificationToken",
                    Token(await holdPage.Content.ReadAsStringAsync()))));
            Assert.Equal(HttpStatusCode.Redirect, holdResponse.StatusCode);
            Assert.Equal("/tai-khoan/yeu-cau-phong",
                holdResponse.Headers.Location?.ToString());

            int holdId;
            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                Assert.Single(await db.YeuCauXemPhongs.Where(v =>
                    v.KhachVangLai!.NguoiDung.TenDangNhap == username).ToListAsync());
                holdId = Assert.Single(await db.YeuCauGiuChos.Where(h =>
                    h.KhachVangLai.NguoiDung.TenDangNhap == username).ToListAsync())
                    .YeuCauGiuChoId;
            }

            var mineResponse = await client.GetAsync("/tai-khoan/yeu-cau-phong");
            Assert.Equal(HttpStatusCode.OK, mineResponse.StatusCode);
            var mineHtml = WebUtility.HtmlDecode(
                await mineResponse.Content.ReadAsStringAsync());
            Assert.Contains("Đã hủy", mineHtml);
            Assert.Contains("Chờ nhân viên duyệt", mineHtml);
            cancelResponse = await client.PostAsync(
                $"/tai-khoan/yeu-cau-phong/giu-cho/{holdId}/huy",
                Form(("__RequestVerificationToken", Token(mineHtml))));
            Assert.Equal(HttpStatusCode.Redirect, cancelResponse.StatusCode);
            var cancelledDepositHtml = WebUtility.HtmlDecode(await
                (await client.GetAsync($"/tai-khoan/yeu-cau-phong/giu-cho/{holdId}/dat-coc"))
                .Content.ReadAsStringAsync());
            Assert.Contains("Yêu cầu đã hủy", cancelledDepositHtml);
            Assert.DoesNotContain("Chờ nhân viên duyệt", cancelledDepositHtml);

            holdResponse = await client.PostAsync($"/phong/{roomId}/giu-cho",
                Form(("__RequestVerificationToken",
                    Token(await holdPage.Content.ReadAsStringAsync()))));
            Assert.Equal(HttpStatusCode.Redirect, holdResponse.StatusCode);
            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                holdId = await db.YeuCauGiuChos.Where(h =>
                    h.KhachVangLai.NguoiDung.TenDangNhap == username &&
                    h.TrangThai == TrangThaiYeuCauGiuCho.MoiTao)
                    .Select(h => h.YeuCauGiuChoId).SingleAsync();
            }
            using (var scope = factory.Services.CreateScope())
            {
                var reservationService = scope.ServiceProvider
                    .GetRequiredService<IReservationService>();
                var now = DateTime.UtcNow;
                Assert.True((await reservationService.ApproveAsync(adminId.Value,
                    holdId, 200000m, now.AddDays(1), now.AddDays(2))).Success);
            }
            mineHtml = await (await client.GetAsync("/tai-khoan/yeu-cau-phong"))
                .Content.ReadAsStringAsync();
            var depositPath = $"/tai-khoan/yeu-cau-phong/giu-cho/{holdId}/dat-coc";
            Assert.Contains(depositPath, mineHtml);
            var depositResponse = await client.GetAsync(depositPath);
            Assert.Equal(HttpStatusCode.OK, depositResponse.StatusCode);
            var depositHtml = WebUtility.HtmlDecode(
                await depositResponse.Content.ReadAsStringAsync());
            Assert.Contains("Thông tin chuyển khoản", depositHtml);
            Assert.Contains("name=\"paymentRequestId\"", depositHtml);
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
                var paymentIds = await db.YeuCauThanhToanGiuChos.Where(p =>
                    holdIds.Contains(p.YeuCauGiuChoId))
                    .Select(p => p.YeuCauThanhToanGiuChoId).ToArrayAsync();
                await db.LichSuTrangThaiYeuCauThanhToanGiuChos.Where(h =>
                    paymentIds.Contains(h.YeuCauThanhToanGiuChoId)).ExecuteDeleteAsync();
                await db.YeuCauThanhToanGiuChos.Where(p =>
                    paymentIds.Contains(p.YeuCauThanhToanGiuChoId)).ExecuteDeleteAsync();
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
            if (adminId.HasValue)
                await db.NguoiDungs.Where(u => u.NguoiDungId == adminId.Value)
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

    private static string Hidden(string html, string name)
    {
        var match = Regex.Match(html, $"name=\"{name}\"[^>]*value=\"([^\"]*)\"");
        Assert.True(match.Success);
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }
}
