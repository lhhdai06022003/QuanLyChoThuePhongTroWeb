using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Security;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence;
using QuanLyChoThuePhongTroWeb.Web.IntegrationTests.Fixtures;

namespace QuanLyChoThuePhongTroWeb.Web.IntegrationTests;

[Collection(WebTestCollection.Name)]
public sealed class RequestedViewingStaffFlowTests(CustomWebApplicationFactory factory)
{
    [Fact]
    public async Task StaffCanContactAndCancelViewingWhenRoomBecomesRented()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var staff = new NguoiDung
        {
            TenDangNhap = "call_staff_" + suffix, Role = Role.Admin,
            MatKhauHash = scope.ServiceProvider.GetRequiredService<IPasswordService>()
                .HashPassword("StaffTest123!")
        };
        var branch = new ChiNhanh { MaChiNhanh = "C" + suffix, TenChiNhanh = "Contact " + suffix,
            DiaChi = "Test", MoTa = "Test", SoDienThoai = "0900000001" };
        var room = new PhongTro { ChiNhanh = branch, SoPhong = "C" + suffix,
            GiaThue = 1000000m, DienTich = 20, MoTa = "Test", TrangThai = TrangThaiPhong.Trong,
            DuocDangTin = true };
        var desired = DateTime.UtcNow.AddDays(2);
        var viewing = new YeuCauXemPhong { PhongTro = room, HoTen = "Khách " + suffix,
            SoDienThoai = "0912345678", Email = "contact_" + suffix + "@example.com",
            ThoiGianMongMuon = desired, NguonTao = NguonTaoYeuCauXemPhong.TrangCongKhai,
            TrangThai = TrangThaiYeuCauXemPhong.ChoNhanVienXuLy, NgayTao = DateTime.UtcNow };
        var oldSlot = new KhungGioXemPhong { PhongTro = room, NguoiTao = staff,
            ThoiGianBatDau = desired, ThoiGianKetThuc = desired.AddHours(1), SoLuongToiDa = 1 };
        db.YeuCauXemPhongs.Add(viewing);
        db.KhungGioXemPhongs.Add(oldSlot);
        await db.SaveChangesAsync();
        try
        {
            using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
                { AllowAutoRedirect = false, HandleCookies = true });
            var loginHtml = await client.GetStringAsync("/QuanLyNhaTro/DangNhap");
            var login = await client.PostAsync("/QuanLyNhaTro/DangNhap", Form(
                ("__RequestVerificationToken", Token(loginHtml)),
                ("TenDangNhap", staff.TenDangNhap), ("MatKhau", "StaffTest123!")));
            Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
            const string pagePath = "/QuanLyNhaTro/LichXemPhong";
            var page = WebUtility.HtmlDecode(await client.GetStringAsync(pagePath));
            Assert.Contains(viewing.HoTen, page);
            Assert.Contains(room.SoPhong, page);
            Assert.Contains(branch.TenChiNhanh, page);
            Assert.Contains("tel:0912345678", page);
            Assert.Contains(viewing.Email!, page);
            Assert.DoesNotContain("slotPreviewModal", page);
            Assert.DoesNotContain("TaoKhungGio", page);
            Assert.Equal(HttpStatusCode.NotFound,
                (await client.PostAsync(pagePath + "/TaoKhungGio", Form(
                    ("__RequestVerificationToken", Token(page))))).StatusCode);
            var blocked = await client.PostAsync(pagePath + "/XuLyYeuCau", Form(
                ("__RequestVerificationToken", Token(page)),
                ("requestId", viewing.YeuCauXemPhongId.ToString(CultureInfo.InvariantCulture)),
                ("action", "CancelBecauseRented")));
            Assert.Equal(HttpStatusCode.Redirect, blocked.StatusCode);
            await db.Entry(viewing).ReloadAsync();
            Assert.Equal(TrangThaiYeuCauXemPhong.ChoNhanVienXuLy, viewing.TrangThai);

            Assert.Empty(factory.FakeEmailServiceInstance.ViewingEmailCalls.Where(n => n.Email == viewing.Email));
            factory.FakeEmailServiceInstance.ResultByAddress[viewing.Email!] = (false, "SMTP unavailable in test");
            var localTime = new DateTimeOffset(desired, TimeSpan.Zero).ToOffset(TimeSpan.FromHours(7))
                .ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture);
            var confirm = await client.PostAsync(pagePath + "/XuLyYeuCau", Form(
                ("__RequestVerificationToken", Token(page)),
                ("requestId", viewing.YeuCauXemPhongId.ToString(CultureInfo.InvariantCulture)),
                ("action", "Confirm"), ("confirmedTimeLocal", localTime)));
            Assert.Equal(HttpStatusCode.Redirect, confirm.StatusCode);
            await db.Entry(viewing).ReloadAsync();
            Assert.Equal(TrangThaiYeuCauXemPhong.DaXacNhanLich, viewing.TrangThai);
            page = WebUtility.HtmlDecode(await client.GetStringAsync(pagePath));
            Assert.Contains("chưa gửi được email", page);
            var firstNotice = Assert.Single(factory.FakeEmailServiceInstance.ViewingEmailCalls.Where(n => n.Email == viewing.Email));
            Assert.Equal("DaXacNhanLich", firstNotice.Status);
            Assert.Equal(viewing.ThoiGianXacNhan, firstNotice.ConfirmedTimeUtc);
            factory.FakeEmailServiceInstance.ResultByAddress[viewing.Email!] = (true, "");
            var resend = await client.PostAsync(pagePath + "/GuiLaiEmail", Form(
                ("__RequestVerificationToken", Token(page)),
                ("requestId", viewing.YeuCauXemPhongId.ToString(CultureInfo.InvariantCulture))));
            Assert.Equal(HttpStatusCode.Redirect, resend.StatusCode);
            Assert.Equal(2, factory.FakeEmailServiceInstance.ViewingEmailCalls.Count(n => n.Email == viewing.Email));
            Assert.Equal(1, await db.LichSuTrangThaiYeuCauXemPhongs.CountAsync(h => h.YeuCauXemPhongId == viewing.YeuCauXemPhongId));

            room.TrangThai = TrangThaiPhong.DaThue;
            await db.SaveChangesAsync();
            page = WebUtility.HtmlDecode(await client.GetStringAsync(pagePath));
            Assert.Contains("CancelBecauseRented", page);
            Assert.Contains("Phòng đã có người thuê", page);
            var noToken = await client.PostAsync(pagePath + "/XuLyYeuCau", Form(
                ("requestId", viewing.YeuCauXemPhongId.ToString(CultureInfo.InvariantCulture)),
                ("action", "CancelBecauseRented")));
            Assert.Equal(HttpStatusCode.BadRequest, noToken.StatusCode);
            var cancel = await client.PostAsync(pagePath + "/XuLyYeuCau", Form(
                ("__RequestVerificationToken", Token(page)),
                ("requestId", viewing.YeuCauXemPhongId.ToString(CultureInfo.InvariantCulture)),
                ("action", "CancelBecauseRented")));
            Assert.Equal(HttpStatusCode.Redirect, cancel.StatusCode);
            await db.Entry(viewing).ReloadAsync();
            Assert.Equal(TrangThaiYeuCauXemPhong.DaHuy, viewing.TrangThai);
            var history = await db.LichSuTrangThaiYeuCauXemPhongs.SingleAsync(
                h => h.YeuCauXemPhongId == viewing.YeuCauXemPhongId && h.TrangThaiSau == TrangThaiYeuCauXemPhong.DaHuy);
            Assert.Equal(staff.NguoiDungId, history.NguoiThucHienId);
            Assert.Equal("Phòng đã có người thuê.", history.LyDo);
            var cancellationNotice = Assert.Single(factory.FakeEmailServiceInstance.ViewingEmailCalls.Where(n => n.Email == viewing.Email && n.Status == "DaHuy"));
            Assert.Equal(history.LyDo, cancellationNotice.LastReason);
            Assert.True(await db.KhungGioXemPhongs.AnyAsync(s => s.KhungGioXemPhongId == oldSlot.KhungGioXemPhongId));
            Assert.Contains("Đã hủy", WebUtility.HtmlDecode(await client.GetStringAsync(pagePath)));
        }
        finally
        {
            await db.LichSuTrangThaiYeuCauXemPhongs.Where(h =>
                h.YeuCauXemPhongId == viewing.YeuCauXemPhongId).ExecuteDeleteAsync();
            await db.YeuCauXemPhongs.Where(v => v.YeuCauXemPhongId == viewing.YeuCauXemPhongId).ExecuteDeleteAsync();
            await db.KhungGioXemPhongs.Where(s => s.KhungGioXemPhongId == oldSlot.KhungGioXemPhongId).ExecuteDeleteAsync();
            await db.PhongTros.Where(r => r.PhongTroId == room.PhongTroId).ExecuteDeleteAsync();
            await db.ChiNhanhs.Where(b => b.ChiNhanhId == branch.ChiNhanhId).ExecuteDeleteAsync();
            await db.NguoiDungs.Where(u => u.NguoiDungId == staff.NguoiDungId).ExecuteDeleteAsync();
        }
    }

    private static FormUrlEncodedContent Form(params (string Key, string Value)[] fields) =>
        new(fields.Select(f => new KeyValuePair<string, string>(f.Key, f.Value)));

    private static string Token(string html)
    {
        var match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(match.Success, "Antiforgery token missing.");
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }
}
