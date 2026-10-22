using System.Net;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Security;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Services;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence;
using QuanLyChoThuePhongTroWeb.Web.IntegrationTests.Fixtures;

namespace QuanLyChoThuePhongTroWeb.Web.IntegrationTests;

[Collection(WebTestCollection.Name)]
public sealed class RoomListingUploadTests(CustomWebApplicationFactory factory)
{
    [Fact]
    public async Task StaffCanSaveListingAndPhotosWithValidationAndBranchIsolation()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var password = scope.ServiceProvider.GetRequiredService<IPasswordService>();
        var admin = new NguoiDung { TenDangNhap = "photo_admin_" + suffix, Role = Role.Admin,
            MatKhauHash = password.HashPassword("UploadTest123!") };
        var outsider = new NguoiDung { TenDangNhap = "photo_staff_" + suffix, Role = Role.NhanVien,
            MatKhauHash = password.HashPassword("UploadTest123!") };
        var branch = new ChiNhanh { MaChiNhanh = "P" + suffix, TenChiNhanh = "Photo " + suffix,
            DiaChi = "Test", MoTa = "Test", SoDienThoai = "0900000000" };
        var room = new PhongTro { ChiNhanh = branch, SoPhong = "P" + suffix, GiaThue = 1000000,
            DienTich = 20, MoTa = "Old description", TrangThai = TrangThaiPhong.Trong };
        db.AddRange(admin, outsider, room);
        await db.SaveChangesAsync();
        var uploads = 0;
        var previousUpload = factory.FakeMeterImageStorageInstance.OnUpload;
        factory.FakeMeterImageStorageInstance.OnUpload = (file, folder) =>
        {
            uploads++;
            Assert.Equal("room_listings", folder);
            return Task.FromResult(new MeterImageUploadResult(
                "https://example.test/room/" + suffix + ".png", "room_listings/" + suffix));
        };
        try
        {
            using var client = await Login(admin.TenDangNhap);
            const string path = "/QuanLyNhaTro/TinPhong";
            var html = await client.GetStringAsync(path);
            var token = Token(html);
            var form = Regex.Match(html, "<form[^>]*id=\"listingForm\"[\\s\\S]*?</form>").Value;
            Assert.NotEmpty(form);
            Assert.Contains("multipart/form-data", form);
            Assert.DoesNotContain("disabled", Regex.Match(form, "<button[^>]*type=\"submit\"[^>]*>").Value);
            var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAusB9Wl6mN0AAAAASUVORK5CYII=");
            using (var noCsrf = Upload(null, png))
                Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync(path + "/LuuTin", noCsrf)).StatusCode);
            using (var invalid = Upload(token, new byte[] { 1, 2, 3, 4 }))
                Assert.Equal(HttpStatusCode.Redirect, (await client.PostAsync(path + "/LuuTin", invalid)).StatusCode);
            Assert.Equal(0, uploads);
            using (var content = Upload(token, png))
                Assert.Equal(HttpStatusCode.Redirect, (await client.PostAsync(path + "/LuuTin", content)).StatusCode);
            Assert.Equal(1, uploads);
            await db.Entry(room).ReloadAsync();
            Assert.Equal("Room title " + suffix, room.TieuDeDangTin);
            Assert.Equal("New description " + suffix, room.MoTa);
            Assert.True(room.DuocDangTin);
            var photo = Assert.Single(await db.AnhPhongTros.Where(p => p.PhongTroId == room.PhongTroId).ToListAsync());
            Assert.True(photo.LaAnhDaiDien);
            Assert.True(photo.IsActive);
            Assert.Equal(admin.NguoiDungId, photo.NguoiTaiLenId);
            Assert.Equal("room_listings/" + suffix, photo.PublicId);
            var refreshed = await client.GetStringAsync(path);
            Assert.Contains("New description " + suffix, refreshed);
            Assert.Contains("Room title " + suffix, refreshed);
            Assert.Contains(photo.Url, await client.GetStringAsync("/phong/" + room.PhongTroId));
            Assert.Contains("Room title " + suffix, await client.GetStringAsync("/phong/" + room.PhongTroId));

            using var denied = await Login(outsider.TenDangNhap);
            var deniedHtml = await denied.GetStringAsync(path);
            using (var content = Upload(Token(deniedHtml), png))
                {
                var forbidden = await denied.PostAsync(path + "/LuuTin", content);
                Assert.Equal(HttpStatusCode.Redirect, forbidden.StatusCode);
                Assert.StartsWith("/QuanLyNhaTro/DangNhap", forbidden.Headers.Location!.PathAndQuery);
                }
            Assert.Equal(1, uploads);

            for (var i = 1; i < 10; i++) db.AnhPhongTros.Add(new AnhPhongTro {
                PhongTroId = room.PhongTroId, NguoiTaiLenId = admin.NguoiDungId,
                Url = "https://example.test/existing.png", PublicId = "existing_" + suffix + i, ThuTuHienThi = i });
            await db.SaveChangesAsync();
            using (var full = Upload(token, png))
                Assert.Equal(HttpStatusCode.Redirect, (await client.PostAsync(path + "/LuuTin", full)).StatusCode);
            Assert.Equal(1, uploads);
            Assert.Equal(10, await db.AnhPhongTros.CountAsync(p => p.PhongTroId == room.PhongTroId));

            MultipartFormDataContent Upload(string? csrf, byte[] bytes)
            {
                var content = new MultipartFormDataContent();
                if (csrf != null) content.Add(new StringContent(csrf), "__RequestVerificationToken");
                content.Add(new StringContent(room.PhongTroId.ToString()), "RoomId");
                content.Add(new StringContent("Room title " + suffix), "Title");
                content.Add(new StringContent("New description " + suffix), "Description");
                content.Add(new StringContent("true"), "Publish");
                var image = new ByteArrayContent(bytes);
                image.Headers.ContentType = new MediaTypeHeaderValue("image/png");
                content.Add(image, "Photos", "room.png");
                return content;
            }
        }
        finally
        {
            factory.FakeMeterImageStorageInstance.OnUpload = previousUpload;
            await db.AnhPhongTros.Where(p => p.PhongTroId == room.PhongTroId).ExecuteDeleteAsync();
            await db.PhongTros.Where(p => p.PhongTroId == room.PhongTroId).ExecuteDeleteAsync();
            await db.ChiNhanhs.Where(b => b.ChiNhanhId == branch.ChiNhanhId).ExecuteDeleteAsync();
            await db.NguoiDungs.Where(u => u.NguoiDungId == admin.NguoiDungId || u.NguoiDungId == outsider.NguoiDungId).ExecuteDeleteAsync();
        }
    }

    private async Task<HttpClient> Login(string username)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        var html = await client.GetStringAsync("/QuanLyNhaTro/DangNhap");
        var response = await client.PostAsync("/QuanLyNhaTro/DangNhap", new FormUrlEncodedContent(new Dictionary<string,string> {
            ["__RequestVerificationToken"] = Token(html), ["TenDangNhap"] = username, ["MatKhau"] = "UploadTest123!" }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return client;
    }

    private static string Token(string html)
    {
        var match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(match.Success);
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }
}
