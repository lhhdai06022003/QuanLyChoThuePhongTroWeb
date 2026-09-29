using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuanLyChoThuePhongTroWeb.Application.Features.GiuChos.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.GiuChos.Services;
using QuanLyChoThuePhongTroWeb.Application.Features.GiuChos.UseCases;
using QuanLyChoThuePhongTroWeb.Application.Features.KhachVangLais.Services;
using QuanLyChoThuePhongTroWeb.Application.Features.NguoiDungs.Services;
using QuanLyChoThuePhongTroWeb.Application.Features.NguoiDungs.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Common.Enums;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Security;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence;
using QuanLyChoThuePhongTroWeb.Web.IntegrationTests.Fixtures;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Web.IntegrationTests;

[Collection(WebTestCollection.Name)]
public sealed class GuestJourneyTests(CustomWebApplicationFactory factory)
{
    [Fact]
    public async Task GuestLoginUsesExistingAccountPage()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var legacy = await client.GetAsync("/tai-khoan/dang-nhap?returnUrl=%2Fgiu-cho");
        Assert.Equal(HttpStatusCode.Redirect, legacy.StatusCode);
        Assert.Contains("/QuanLyNhaTro/DangNhap", legacy.Headers.Location!.ToString());
        var challenge = await client.GetAsync("/giu-cho");
        Assert.Contains("/QuanLyNhaTro/DangNhap", challenge.Headers.Location!.ToString());
    }

    [Fact]
    public async Task TenantLoginReturnsToSelectedBookingPageAndShowsIdentity()
    {
        var key = Guid.NewGuid().ToString("N");
        var username = "tenant_http_" + key;
        const string password = "TenantPassword123!";
        int userId = 0, tenantId = 0, roomId = 0, branchId = 0;
        try
        {
            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var tenant = new NguoiThue
                {
                    HoVaTen = "Tenant HTTP", Email = username + "@example.test",
                    SoDienThoai = "0900000002",
                    CCCD = Random.Shared.NextInt64(100_000_000_000, 999_999_999_999).ToString()
                };
                var user = new NguoiDung
                {
                    TenDangNhap = username,
                    MatKhauHash = scope.ServiceProvider.GetRequiredService<IPasswordService>()
                        .HashPassword(password),
                    Role = Role.KhachThue, NguoiThue = tenant
                };
                var branch = new ChiNhanh { MaChiNhanh = "T" + key[..8],
                    TenChiNhanh = "Tenant HTTP branch", DiaChi = "Test address",
                    SoDienThoai = "0900000000", MoTa = "Test" };
                var room = new PhongTro { ChiNhanh = branch, SoPhong = "T" + key[..6],
                    GiaThue = 2_000_000, DienTich = 20, SoNguoiToiDa = 2,
                    TrangThai = TrangThaiPhong.Trong, DuocDangTin = true,
                    MaCongKhai = key, MoTa = "Tenant HTTP test room" };
                db.NguoiDungs.Add(user);
                db.PhongTros.Add(room);
                await db.SaveChangesAsync();
                userId = user.NguoiDungId; tenantId = tenant.NguoiThueId;
                roomId = room.PhongTroId; branchId = branch.ChiNhanhId;
            }

            foreach (var destination in new[] { "/lich-xem/" + key, "/giu-cho/tao/" + key })
            {
                using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
                { AllowAutoRedirect = false, HandleCookies = true, BaseAddress = new Uri("https://localhost") });
                var challenge = await client.GetAsync(destination);
                Assert.Equal(HttpStatusCode.Redirect, challenge.StatusCode);
                Assert.Contains("/QuanLyNhaTro/DangNhap", challenge.Headers.Location!.ToString());
                Assert.Contains(Uri.EscapeDataString(destination), challenge.Headers.Location!.ToString(),
                    StringComparison.OrdinalIgnoreCase);
                var loginPath = "/QuanLyNhaTro/DangNhap?returnUrl=" + Uri.EscapeDataString(destination);
                var login = await PostFormAsync(client, loginPath,
                    new() { ["TenDangNhap"] = username, ["MatKhau"] = password });
                Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
                Assert.Equal(destination, login.Headers.Location!.ToString());
                Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(destination)).StatusCode);
                Assert.Contains("Đang đăng nhập: <strong>" + username + "</strong>",
                    await (await client.GetAsync("/phong")).Content.ReadAsStringAsync());
                var dashboard = await client.GetAsync("/KhachThue/Dashboard");
                Assert.Equal(HttpStatusCode.OK, dashboard.StatusCode);
                var dashboardHtml = await dashboard.Content.ReadAsStringAsync();
                Assert.Contains("/lich-xem", dashboardHtml);
                Assert.Contains("/giu-cho", dashboardHtml);
            }
        }
        finally
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            if (roomId != 0) await db.PhongTros.Where(item => item.PhongTroId == roomId).ExecuteDeleteAsync();
            if (userId != 0) await db.NguoiDungs.Where(item => item.NguoiDungId == userId).ExecuteDeleteAsync();
            if (tenantId != 0) await db.NguoiThues.Where(item => item.NguoiThueId == tenantId).ExecuteDeleteAsync();
            if (branchId != 0) await db.ChiNhanhs.Where(item => item.ChiNhanhId == branchId).ExecuteDeleteAsync();
        }
    }

    [Fact]
    public async Task NewGuestCanRegisterAndReturnToSelectedRoom()
    {
        var username = "booking_" + Guid.NewGuid().ToString("N");
        var returnUrl = "/giu-cho/tao/room-selected";
        var cccd = Random.Shared.NextInt64(100_000_000_000, 999_999_999_999).ToString();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        { AllowAutoRedirect = false, HandleCookies = true, BaseAddress = new Uri("https://localhost") });
        try
        {
            var loginPage = await client.GetAsync("/QuanLyNhaTro/DangNhap?returnUrl=" +
                Uri.EscapeDataString(returnUrl));
            Assert.Equal(HttpStatusCode.OK, loginPage.StatusCode);
            Assert.Contains("/tai-khoan/dang-ky", await loginPage.Content.ReadAsStringAsync());

            var registrationPath = "/tai-khoan/dang-ky?returnUrl=" + Uri.EscapeDataString(returnUrl);
            var registrationPage = await client.GetAsync(registrationPath);
            Assert.Equal(HttpStatusCode.OK, registrationPage.StatusCode);
            Assert.Contains("name=\"ReturnUrl\"", await registrationPage.Content.ReadAsStringAsync());
            var externalPage = await client.GetAsync(
                "/tai-khoan/dang-ky?returnUrl=https%3A%2F%2Foutside.example%2F");
            Assert.DoesNotContain("https://outside.example/",
                await externalPage.Content.ReadAsStringAsync());

            var registered = await PostFormAsync(client, registrationPath, new()
            {
                ["TenDangNhap"] = username,
                ["MatKhau"] = "GuestPassword123!",
                ["NhapLaiMatKhau"] = "GuestPassword123!",
                ["HoTen"] = "Booking guest",
                ["SoDienThoai"] = "0900000001",
                ["Email"] = username + "@example.test",
                ["CCCD"] = cccd,
                ["ReturnUrl"] = returnUrl
            });
            Assert.Equal(HttpStatusCode.Redirect, registered.StatusCode);
            Assert.Contains("returnUrl", registered.Headers.Location!.ToString(), StringComparison.OrdinalIgnoreCase);

            var signedIn = await PostFormAsync(client,
                "/QuanLyNhaTro/DangNhap?returnUrl=" + Uri.EscapeDataString(returnUrl),
                new() { ["TenDangNhap"] = username, ["MatKhau"] = "GuestPassword123!" });
            Assert.Equal(HttpStatusCode.Redirect, signedIn.StatusCode);
            Assert.Equal(returnUrl, signedIn.Headers.Location!.ToString());
        }
        finally
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.KhachVangLais.Where(item => item.NguoiDung.TenDangNhap == username)
                .ExecuteDeleteAsync();
            await db.NguoiDungs.Where(item => item.TenDangNhap == username).ExecuteDeleteAsync();
        }
    }

    [Fact]
    public async Task GuestHttpJourney_ProtectsOwnershipCsrfAndCookieIdentity()
    {
        var key = Guid.NewGuid().ToString("N");
        var username = "http_" + key;
        const string password = "GuestPassword123!";
        int actorId = 0, branchId = 0, roomId = 0, holdId = 0, secondId = 0, staffId = 0;
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        { AllowAutoRedirect = false, HandleCookies = true, BaseAddress = new Uri("https://localhost") });
        using var anonymous = factory.CreateClient(new WebApplicationFactoryClientOptions
        { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
        try
        {
            using (var scope = factory.Services.CreateScope())
            {
                var registered = await scope.ServiceProvider.GetRequiredService<IKhachVangLaiService>()
                    .RegisterAsync(new(username, password, "HTTP guest", "0900000001",
                        username + "@example.test", Random.Shared.NextInt64(100_000_000_000, 999_999_999_999).ToString()));
                Assert.True(registered.Success, registered.Message);
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                actorId = await db.NguoiDungs.Where(item => item.TenDangNhap == username)
                    .Select(item => item.NguoiDungId).SingleAsync();
                var branch = new ChiNhanh { MaChiNhanh = "H" + key[..8], TenChiNhanh = "HTTP branch",
                    DiaChi = "Test address", SoDienThoai = "0900000000", MoTa = "Test" };
                var room = new PhongTro { ChiNhanh = branch, SoPhong = "H" + key[..6], GiaThue = 2_000_000,
                    DienTich = 20, SoNguoiToiDa = 2, TrangThai = TrangThaiPhong.Trong,
                    DuocDangTin = true, MaCongKhai = key, MoTa = "HTTP test room" };
                db.PhongTros.Add(room);
                await db.SaveChangesAsync();
                branchId = branch.ChiNhanhId; roomId = room.PhongTroId;
                var uploaderId = await db.NguoiDungs.Where(item => item.Role == Role.Admin &&
                    item.IsActive && !item.IsDeleted).Select(item => item.NguoiDungId).FirstAsync();
                for (var index = 0; index < 6; index++)
                    db.AnhPhongTros.Add(new AnhPhongTro
                    {
                        PhongTroId = roomId, Url = $"https://example.test/room-{index}.jpg",
                        PublicId = "journey-" + key + "-" + index, ThuTuHienThi = index,
                        LaAnhDaiDien = index == 0, IsActive = true, NguoiTaiLenId = uploaderId
                    });
                db.AnhPhongTros.Add(new AnhPhongTro
                {
                    PhongTroId = roomId, Url = "https://example.test/hidden.jpg",
                    PublicId = "journey-" + key + "-hidden", ThuTuHienThi = 7,
                    IsActive = false, NguoiTaiLenId = uploaderId
                });
                await db.SaveChangesAsync();
            }

            var publicPage = await anonymous.GetAsync("/phong/" + key);
            Assert.Equal(HttpStatusCode.OK, publicPage.StatusCode);
            var publicHtml = await publicPage.Content.ReadAsStringAsync();
            Assert.DoesNotContain(username + "@example.test", publicHtml);
            Assert.Equal(6, Regex.Matches(publicHtml, "data-gallery-thumb").Count);
            Assert.Contains("data-gallery-next", publicHtml);
            Assert.DoesNotContain("hidden.jpg", publicHtml);
            var viewingChallenge = await anonymous.GetAsync("/lich-xem/" + key);
            Assert.Equal(HttpStatusCode.Redirect, viewingChallenge.StatusCode);
            Assert.Contains("/QuanLyNhaTro/DangNhap", viewingChallenge.Headers.Location!.ToString());
            Assert.Contains("ReturnUrl", viewingChallenge.Headers.Location!.ToString());
            var viewingPostChallenge = await anonymous.PostAsync("/lich-xem/" + key,
                new FormUrlEncodedContent(new Dictionary<string, string>()));
            Assert.Equal(HttpStatusCode.Redirect, viewingPostChallenge.StatusCode);
            Assert.Contains("/QuanLyNhaTro/DangNhap", viewingPostChallenge.Headers.Location!.ToString());
            var challenge = await anonymous.GetAsync("/giu-cho/tao/" + key);
            Assert.Contains("/QuanLyNhaTro/DangNhap", challenge.Headers.Location!.ToString());

            var login = await PostFormAsync(client, "/QuanLyNhaTro/DangNhap", new()
            { ["TenDangNhap"] = username, ["MatKhau"] = password,
                ["ReturnUrl"] = "https://outside.example/", ["Role"] = "Admin" });
            Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
            Assert.Equal("/phong", login.Headers.Location!.ToString());
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/lich-xem/" + key)).StatusCode);
            Assert.NotEqual(HttpStatusCode.OK, (await client.GetAsync("/QuanLyNhaTro/Dashboard")).StatusCode);
            var create = await PostFormAsync(client, "/giu-cho/tao/" + key, new());
            Assert.Equal(HttpStatusCode.Redirect, create.StatusCode);
            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                holdId = await db.YeuCauGiuChos.Where(item => item.PhongTroId == roomId)
                    .Select(item => item.YeuCauGiuChoId).SingleAsync();
            }
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/giu-cho/" + holdId)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/giu-cho/" + int.MaxValue)).StatusCode);
            Assert.Equal(HttpStatusCode.Redirect, (await anonymous.GetAsync("/minh-chung-giu-cho/1")).StatusCode);

            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var adminId = await db.NguoiDungs.Where(item => item.Role == Role.Admin && item.IsActive && !item.IsDeleted)
                    .Select(item => item.NguoiDungId).FirstAsync();
                var approved = await scope.ServiceProvider.GetRequiredService<IGiuChoService>().ApproveAsync(holdId,
                    new(DateTimeOffset.UtcNow.AddMinutes(30), DateTimeOffset.UtcNow.AddDays(1)), adminId);
                Assert.True(approved.Success, approved.Message);
            }
            Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync("/phong/" + key)).StatusCode);
            var paymentPage = await client.GetAsync("/thanh-toan-giu-cho/" + holdId);
            Assert.Equal(HttpStatusCode.OK, paymentPage.StatusCode);
            var paymentHtml = await paymentPage.Content.ReadAsStringAsync();
            int paymentId;
            using (var scope = factory.Services.CreateScope())
                paymentId = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().YeuCauThanhToanGiuChos
                    .Where(item => item.YeuCauGiuChoId == holdId).Select(item => item.YeuCauThanhToanGiuChoId).SingleAsync();
            using var upload = new MultipartFormDataContent();
            upload.Add(new StringContent(Token(paymentHtml)), "__RequestVerificationToken");
            upload.Add(new StringContent(paymentId.ToString()), "paymentId");
            upload.Add(new StringContent("1000000"), "soTienKhaiBao");
            upload.Add(new StringContent(DateTime.UtcNow.AddHours(7).ToString("yyyy-MM-ddTHH:mm")), "ngayChuyenTien");
            var png = new ByteArrayContent(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10, 1, 2, 3 });
            png.Headers.ContentType = new("image/png");
            upload.Add(png, "image", "test-proof.png");
            Assert.Equal(HttpStatusCode.Redirect, (await client.PostAsync("/thanh-toan-giu-cho/" + holdId + "/minh-chung", upload)).StatusCode);
            int proofId;
            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                proofId = await db.MinhChungThanhToanGiuChos.Where(item => item.YeuCauThanhToanGiuChoId == paymentId)
                    .Select(item => item.MinhChungThanhToanGiuChoId).SingleAsync();
                var registered = await scope.ServiceProvider.GetRequiredService<IKhachVangLaiService>()
                    .RegisterAsync(new("other_" + key, password, "Other visitor", "0900000002",
                        "other_" + key + "@example.test", Random.Shared.NextInt64(100_000_000_000, 999_999_999_999).ToString()));
                Assert.True(registered.Success, registered.Message);
                secondId = await db.NguoiDungs.Where(item => item.TenDangNhap == "other_" + key)
                    .Select(item => item.NguoiDungId).SingleAsync();
            }
            var proof = await client.GetAsync("/minh-chung-giu-cho/" + proofId);
            Assert.Equal(HttpStatusCode.OK, proof.StatusCode);
            Assert.True(proof.Headers.CacheControl!.NoStore);
            using var other = factory.CreateClient(new WebApplicationFactoryClientOptions
            { AllowAutoRedirect = false, HandleCookies = true, BaseAddress = new Uri("https://localhost") });
            Assert.Equal(HttpStatusCode.Redirect, (await PostFormAsync(other, "/QuanLyNhaTro/DangNhap",
                new() { ["TenDangNhap"] = "other_" + key, ["MatKhau"] = password })).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync("/minh-chung-giu-cho/" + proofId)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync("/giu-cho/" + holdId)).StatusCode);
            using (var scope = factory.Services.CreateScope())
            {
                var created = await scope.ServiceProvider.GetRequiredService<INguoiDungService>().AddAsync(new CreateNguoiDungReq
                { TenDangNhap = "staff_" + key, MatKhau = password, Role = AppRole.Admin });
                Assert.True(created.Success, created.Message);
                staffId = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().NguoiDungs
                    .Where(item => item.TenDangNhap == "staff_" + key).Select(item => item.NguoiDungId).SingleAsync();
            }
            using var staff = factory.CreateClient(new WebApplicationFactoryClientOptions
            { AllowAutoRedirect = false, HandleCookies = true, BaseAddress = new Uri("https://localhost") });
            Assert.Equal(HttpStatusCode.Redirect, (await PostFormAsync(staff, "/QuanLyNhaTro/DangNhap",
                new() { ["TenDangNhap"] = "staff_" + key, ["MatKhau"] = password })).StatusCode);
            foreach (var route in new[] { "TinPhong", "LichXemPhong", "KhungGioXemPhong", "GiuCho", "DoiChieuGiuCho", "HoanTienGiuCho" })
                Assert.Equal(HttpStatusCode.OK, (await staff.GetAsync("/QuanLyNhaTro/" + route)).StatusCode);
            var photoPath = "/QuanLyNhaTro/TinPhong/Anh/" + roomId;
            var photoPage = await staff.GetAsync(photoPath);
            Assert.Equal(HttpStatusCode.OK, photoPage.StatusCode);
            using (var photoUpload = new MultipartFormDataContent())
            {
                photoUpload.Add(new StringContent(Token(await photoPage.Content.ReadAsStringAsync())),
                    "__RequestVerificationToken");
                for (var index = 0; index < 2; index++)
                {
                    var image = new ByteArrayContent(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10, 1, 2, 3 });
                    image.Headers.ContentType = new("image/png");
                    photoUpload.Add(image, "images", $"room-{index}.png");
                }
                var photoResult = await staff.PostAsync(
                    "/QuanLyNhaTro/TinPhong/TaiAnh/" + roomId, photoUpload);
                Assert.Equal(HttpStatusCode.Redirect, photoResult.StatusCode);
            }
            using (var scope = factory.Services.CreateScope())
                Assert.Equal(8, await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
                    .AnhPhongTros.CountAsync(image => image.PhongTroId == roomId && image.IsActive));
            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var adminId = await db.NguoiDungs.Where(item => item.Role == Role.Admin && item.IsActive && !item.IsDeleted)
                    .Select(item => item.NguoiDungId).FirstAsync();
                var confirmed = await scope.ServiceProvider.GetRequiredService<IThanhToanGiuChoService>()
                    .ConfirmAsync(proofId, adminId, "HTTP-BANK-" + key, 1_000_000, DateTimeOffset.UtcNow);
                Assert.True(confirmed.Success, confirmed.Message);
                Assert.Equal(HttpStatusCode.OK, (await staff.GetAsync("/QuanLyNhaTro/ChuyenHopDong/Tao/" + holdId)).StatusCode);
                var converted = await scope.ServiceProvider.GetRequiredService<IChuyenHopDongTuGiuChoUseCase>()
                    .ExecuteAsync(holdId, new(DateTimeOffset.UtcNow, null, []), adminId);
                Assert.True(converted.Success, converted.Message);
            }
            // Existing cookie must pick up the persisted role on the next request.
            Assert.NotEqual(HttpStatusCode.OK, (await client.GetAsync("/giu-cho/tao/" + key)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/giu-cho/" + holdId)).StatusCode);
            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                (await db.NguoiDungs.FindAsync(actorId))!.IsActive = false;
                await db.SaveChangesAsync();
            }
            Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/giu-cho/" + holdId)).StatusCode);
        }
        finally
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var tenantIds = await db.NguoiDungs.Where(item => item.NguoiDungId == actorId && item.NguoiThueId != null)
                .Select(item => item.NguoiThueId!.Value).ToListAsync();
            var payments = db.YeuCauThanhToanGiuChos.Where(item => item.YeuCauGiuChoId == holdId).Select(item => item.YeuCauThanhToanGiuChoId);
            await db.GiaoDichGiuChos.Where(item => payments.Contains(item.YeuCauThanhToanGiuChoId)).ExecuteDeleteAsync();
            await db.MinhChungThanhToanGiuChos.Where(item => payments.Contains(item.YeuCauThanhToanGiuChoId)).ExecuteDeleteAsync();
            await db.LichSuTrangThaiYeuCauThanhToanGiuChos.Where(item => payments.Contains(item.YeuCauThanhToanGiuChoId)).ExecuteDeleteAsync();
            await db.YeuCauThanhToanGiuChos.Where(item => item.YeuCauGiuChoId == holdId).ExecuteDeleteAsync();
            await db.ApDungTienGiuChoVaoTienCocs.Where(item => item.YeuCauGiuChoId == holdId).ExecuteDeleteAsync();
            var contracts = db.HopDongs.Where(item => item.PhongTroId == roomId).Select(item => item.HopDongId);
            await db.ChiTietThanhVienHopDongs.Where(item => contracts.Contains(item.HopDongId)).ExecuteDeleteAsync();
            await db.HopDongDieuKhoans.Where(item => contracts.Contains(item.HopDongId)).ExecuteDeleteAsync();
            await db.HopDongs.Where(item => item.PhongTroId == roomId).ExecuteDeleteAsync();
            await db.LichSuTrangThaiYeuCauGiuChos.Where(item => item.YeuCauGiuCho.PhongTroId == roomId).ExecuteDeleteAsync();
            await db.YeuCauGiuChos.Where(item => item.PhongTroId == roomId).ExecuteDeleteAsync();
            await db.PhongTros.Where(item => item.PhongTroId == roomId).ExecuteDeleteAsync();
            await db.KhachVangLais.Where(item => item.NguoiDungId == actorId || item.NguoiDungId == secondId).ExecuteDeleteAsync();
            await db.NguoiDungs.Where(item => item.NguoiDungId == actorId || item.NguoiDungId == secondId || item.NguoiDungId == staffId).ExecuteDeleteAsync();
            await db.NguoiThues.Where(item => tenantIds.Contains(item.NguoiThueId)).ExecuteDeleteAsync();
            await db.ChiNhanhs.Where(item => item.ChiNhanhId == branchId).ExecuteDeleteAsync();
        }
    }

    private static async Task<HttpResponseMessage> PostFormAsync(HttpClient client, string path,
        Dictionary<string, string> fields)
    {
        var page = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        var html = await page.Content.ReadAsStringAsync();
        fields["__RequestVerificationToken"] = Token(html);
        Assert.NotEmpty(fields["__RequestVerificationToken"]);
        return await client.PostAsync(path, new FormUrlEncodedContent(fields));
    }

    private static string Token(string html) => Regex.Match(html,
        "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
}
