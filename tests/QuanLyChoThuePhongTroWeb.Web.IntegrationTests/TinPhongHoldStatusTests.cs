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

// Phòng đã bật tin nhưng đang có giữ chỗ bị ẩn khỏi /phong; màn Tin phòng không được báo
// "Đang hiển thị" hay đưa nút "Xem tin" dẫn tới trang 404.
[Collection(WebTestCollection.Name)]
public sealed class TinPhongHoldStatusTests(CustomWebApplicationFactory factory)
{
    private const string Password = "TinPhongHold123!";

    [Fact]
    public async Task PublishedRoomOnHold_IsShownAsHiddenWithoutPublicLink()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var passwords = scope.ServiceProvider.GetRequiredService<IPasswordService>();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var staff = new NguoiDung { TenDangNhap = "tin_hold_" + suffix, Role = Role.NhanVien,
            MatKhauHash = passwords.HashPassword(Password) };
        var guestUser = new NguoiDung { TenDangNhap = "tin_guest_" + suffix, Role = Role.KhachVangLai,
            MatKhauHash = "test" };
        var guest = new KhachVangLai { NguoiDung = guestUser, HoTen = "Khách giữ chỗ",
            SoDienThoai = "0912345678", Email = "tinhold" + suffix + "@example.com" };
        var branch = new ChiNhanh { MaChiNhanh = "H" + suffix, TenChiNhanh = "Hold " + suffix,
            DiaChi = "Test", MoTa = "Test", SoDienThoai = "0900000000" };
        var freeRoom = new PhongTro { ChiNhanh = branch, SoPhong = "F" + suffix, GiaThue = 1000000,
            DienTich = 20, MoTa = "Free", TrangThai = TrangThaiPhong.Trong, DuocDangTin = true };
        var heldRoom = new PhongTro { ChiNhanh = branch, SoPhong = "G" + suffix, GiaThue = 1000000,
            DienTich = 20, MoTa = "Held", TrangThai = TrangThaiPhong.Trong, DuocDangTin = true };
        db.AddRange(staff, guest, freeRoom, heldRoom);
        await db.SaveChangesAsync();
        db.AddRange(
            new NhanVienChiNhanh { NguoiDungId = staff.NguoiDungId, ChiNhanhId = branch.ChiNhanhId,
                NguoiPhanCongId = staff.NguoiDungId },
            new YeuCauGiuCho { KhachVangLaiId = guest.KhachVangLaiId, PhongTroId = heldRoom.PhongTroId,
                SoTienGiuCho = 500000m, HanThanhToan = DateTime.UtcNow.AddDays(-1),
                HanKyHopDong = DateTime.UtcNow.AddDays(5), TrangThai = TrangThaiYeuCauGiuCho.DangGiuCho });
        await db.SaveChangesAsync();
        try
        {
            using var client = await Login(staff.TenDangNhap);

            var html = WebUtility.HtmlDecode(await client.GetStringAsync("/QuanLyNhaTro/TinPhong"));

            var freeRow = Row(html, freeRoom.SoPhong);
            Assert.Contains("Đang hiển thị", freeRow);
            Assert.Contains($"href=\"/phong/{freeRoom.PhongTroId}\"", freeRow);
            var heldRow = Row(html, heldRoom.SoPhong);
            Assert.Contains("Ẩn — đang giữ chỗ", heldRow);
            Assert.DoesNotContain("Đang hiển thị", heldRow);
            Assert.DoesNotContain($"href=\"/phong/{heldRoom.PhongTroId}\"", heldRow);
            Assert.Equal(HttpStatusCode.NotFound,
                (await client.GetAsync($"/phong/{heldRoom.PhongTroId}")).StatusCode);
        }
        finally
        {
            await db.YeuCauGiuChos.Where(h => h.PhongTroId == heldRoom.PhongTroId).ExecuteDeleteAsync();
            await db.NhanVienChiNhanhs.Where(a => a.NguoiDungId == staff.NguoiDungId).ExecuteDeleteAsync();
            await db.PhongTros.Where(p => p.ChiNhanhId == branch.ChiNhanhId).ExecuteDeleteAsync();
            await db.ChiNhanhs.Where(b => b.ChiNhanhId == branch.ChiNhanhId).ExecuteDeleteAsync();
            await db.KhachVangLais.Where(g => g.KhachVangLaiId == guest.KhachVangLaiId).ExecuteDeleteAsync();
            await db.NguoiDungs.Where(u => u.NguoiDungId == staff.NguoiDungId ||
                u.NguoiDungId == guestUser.NguoiDungId).ExecuteDeleteAsync();
        }
    }

    private static string Row(string html, string roomNumber)
    {
        var match = Regex.Match(html, $"<tr>(?:(?!</tr>)[\\s\\S])*Phòng {roomNumber}[\\s\\S]*?</tr>");
        Assert.True(match.Success, $"Không thấy dòng phòng {roomNumber}");
        return match.Value;
    }

    private async Task<HttpClient> Login(string username)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        var html = await client.GetStringAsync("/QuanLyNhaTro/DangNhap");
        var response = await client.PostAsync("/QuanLyNhaTro/DangNhap", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = Token(html), ["TenDangNhap"] = username, ["MatKhau"] = Password
        }));
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
