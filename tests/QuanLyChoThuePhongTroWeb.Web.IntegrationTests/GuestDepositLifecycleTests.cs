using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Security;
using QuanLyChoThuePhongTroWeb.Application.Features.Reservations;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence;
using QuanLyChoThuePhongTroWeb.Web.IntegrationTests.Fixtures;

namespace QuanLyChoThuePhongTroWeb.Web.IntegrationTests;

[Collection(WebTestCollection.Name)]
public sealed class GuestDepositLifecycleTests(CustomWebApplicationFactory factory)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PendingRequests_PreserveLegacyDeadlineAndDeferOnlySelfServiceDeadline(bool selfService)
    {
        await using var data = await RequestData.CreateAsync(factory, selfService);
        using var client = await data.LoginAsync();
        var fixedDeadline = data.ContractDeadlineUtc.AddHours(7).ToString("dd/MM/yyyy HH:mm");

        foreach (var path in new[] { data.DepositPath, "/tai-khoan/yeu-cau-phong", "/KhachThue/Dashboard" })
        {
            var response = await client.GetAsync(path);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
            if (selfService)
            {
                Assert.Contains("Hạn ký hợp đồng: trong 48 giờ sau khi nhận đủ cọc.", html);
                Assert.DoesNotContain("Hạn ký hợp đồng: " + fixedDeadline, html);
            }
            else
            {
                Assert.Contains("Hạn ký hợp đồng: " + fixedDeadline, html);
                Assert.DoesNotContain("Hạn ký hợp đồng: trong 48 giờ sau khi nhận đủ cọc.", html);
            }
        }

    }

    [Theory]
    [InlineData(TrangThaiYeuCauGiuCho.DaHuy, "Yêu cầu đã hủy")]
    [InlineData(TrangThaiYeuCauGiuCho.HetHan, "Yêu cầu đã hết hạn")]
    public async Task ClosedRequests_WithQuotedAmountShowNoticeAndNoCheckout(TrangThaiYeuCauGiuCho status,
        string notice)
    {
        await using var data = await RequestData.CreateAsync(factory, true);
        await data.CloseAsync(status);
        using var client = await data.LoginAsync();

        var response = await client.GetAsync(data.DepositPath);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("375.000 ₫", html);
        Assert.Contains(notice, html);
        Assert.Contains("Vui lòng không chuyển thêm tiền", html);
        Assert.DoesNotContain("id=\"btnGuestVnPay\"", html);
        Assert.DoesNotContain("name=\"paymentRequestId\"", html);
    }

    private sealed class RequestData(CustomWebApplicationFactory factory) : IAsyncDisposable
    {
        private const string Password = "DepositUi123!";
        private readonly string suffix = Guid.NewGuid().ToString("N")[..8];
        private int userId, staffId, guestId, branchId, roomId, reservationId;
        private string Username => "deposit_ui_" + suffix;
        public string DepositPath => $"/tai-khoan/yeu-cau-phong/giu-cho/{reservationId}/dat-coc";
        public DateTime ContractDeadlineUtc { get; private set; }

        public static async Task<RequestData> CreateAsync(CustomWebApplicationFactory factory, bool selfService)
        {
            var data = new RequestData(factory);
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var passwords = scope.ServiceProvider.GetRequiredService<IPasswordService>();
            var user = new NguoiDung { TenDangNhap = data.Username,
                MatKhauHash = passwords.HashPassword(Password), Role = Role.KhachVangLai };
            var staff = new NguoiDung { TenDangNhap = "deposit_staff_" + data.suffix,
                MatKhauHash = "unused", Role = Role.Admin };
            var guest = new KhachVangLai { NguoiDung = user, HoTen = "Khách kiểm thử",
                SoDienThoai = "0912345678" };
            var branch = new ChiNhanh { MaChiNhanh = "D" + data.suffix,
                TenChiNhanh = "Deposit UI " + data.suffix, DiaChi = "Test", MoTa = "Test",
                SoDienThoai = "0912345678" };
            var room = new PhongTro { ChiNhanh = branch, SoPhong = "D" + data.suffix,
                GiaThue = 1500000, DienTich = 20, MoTa = "Test", DuocDangTin = true, TienCocPhong = 375000m,
                TrangThai = TrangThaiPhong.Trong };
            db.AddRange(guest, staff, room);
            await db.SaveChangesAsync();
            data.userId = user.NguoiDungId;
            data.staffId = staff.NguoiDungId;
            data.guestId = guest.KhachVangLaiId;
            data.branchId = branch.ChiNhanhId;
            data.roomId = room.PhongTroId;
            var now = DateTime.UtcNow;
            data.ContractDeadlineUtc = now.Date.AddDays(5).AddHours(3);
            if (selfService)
            {
                var service = scope.ServiceProvider.GetRequiredService<IReservationService>();
                var result = await service.RequestAsync(data.userId, data.roomId, null);
                Assert.True(result.Success, result.Message);
                data.reservationId = result.Data;
                await db.YeuCauGiuChos.Where(h => h.YeuCauGiuChoId == data.reservationId)
                    .ExecuteUpdateAsync(s => s.SetProperty(h => h.HanKyHopDong, data.ContractDeadlineUtc));
            }
            else
            {
                var hold = new YeuCauGiuCho { KhachVangLaiId = data.guestId, PhongTroId = data.roomId,
                    TrangThai = TrangThaiYeuCauGiuCho.ChoThanhToan, SoTienGiuCho = 375000,
                    HanThanhToan = now.AddHours(2), HanKyHopDong = data.ContractDeadlineUtc,
                    NguoiDuyetId = data.staffId, NgayDuyet = now };
                hold.YeuCauThanhToanGiuChos.Add(new YeuCauThanhToanGiuCho { NguoiTaoId = data.staffId,
                    MaYeuCau = "LEGACY-" + data.suffix, NoiDungChuyenKhoan = "LEGACY-" + data.suffix,
                    SoTien = 375000, HanThanhToan = now.AddHours(2), TrangThai = TrangThaiYeuCauThanhToan.ChoThanhToan });
                db.YeuCauGiuChos.Add(hold);
                await db.SaveChangesAsync();
                data.reservationId = hold.YeuCauGiuChoId;
            }
            return data;
        }

        public async Task CloseAsync(TrangThaiYeuCauGiuCho status)
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.YeuCauGiuChos.Where(h => h.YeuCauGiuChoId == reservationId)
                .ExecuteUpdateAsync(s => s.SetProperty(h => h.TrangThai, status));
        }

        public async Task<HttpClient> LoginAsync()
        {
            var client = factory.CreateClient(new WebApplicationFactoryClientOptions {
                AllowAutoRedirect = false, HandleCookies = true });
            var html = await client.GetStringAsync("/QuanLyNhaTro/DangNhap");
            var token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
            Assert.True(token.Success);
            var response = await client.PostAsync("/QuanLyNhaTro/DangNhap", new FormUrlEncodedContent(
                new Dictionary<string, string> { ["__RequestVerificationToken"] = WebUtility.HtmlDecode(token.Groups[1].Value),
                    ["TenDangNhap"] = Username, ["MatKhau"] = Password }));
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            return client;
        }

        public async ValueTask DisposeAsync()
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var payments = db.YeuCauThanhToanGiuChos.Where(p => p.YeuCauGiuChoId == reservationId);
            await db.LichSuTrangThaiYeuCauThanhToanGiuChos.Where(h => payments.Any(p =>
                p.YeuCauThanhToanGiuChoId == h.YeuCauThanhToanGiuChoId)).ExecuteDeleteAsync();
            await payments.ExecuteDeleteAsync();
            await db.LichSuTrangThaiYeuCauGiuChos.Where(h => h.YeuCauGiuChoId == reservationId).ExecuteDeleteAsync();
            await db.YeuCauGiuChos.Where(h => h.YeuCauGiuChoId == reservationId).ExecuteDeleteAsync();
            await db.KhachVangLais.Where(g => g.KhachVangLaiId == guestId).ExecuteDeleteAsync();
            await db.PhongTros.Where(p => p.PhongTroId == roomId).ExecuteDeleteAsync();
            await db.ChiNhanhs.Where(b => b.ChiNhanhId == branchId).ExecuteDeleteAsync();
            await db.NguoiDungs.Where(u => u.NguoiDungId == userId || u.NguoiDungId == staffId).ExecuteDeleteAsync();
        }
    }
}
