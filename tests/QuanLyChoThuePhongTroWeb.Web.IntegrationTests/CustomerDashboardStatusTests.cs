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
public sealed class CustomerDashboardStatusTests(CustomWebApplicationFactory factory)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DashboardShowsOwnedViewingAndDepositProgressForBothCustomerRoles(bool tenant)
    {
        await using var data = await DashboardData.CreateAsync(factory, tenant);
        await data.AddRequestsAsync();
        using var client = CreateClient();
        await LoginAsync(client, data.Username);
        var response = await client.GetAsync("/KhachThue/Dashboard");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        foreach (var label in new[] { "Chờ nhân viên xác nhận", "Đã xác nhận lịch", "Từ chối lịch xem", "Đã hủy lịch xem",
            "Chờ nhân viên duyệt", "Đã duyệt · Chờ chuyển khoản", "Đã báo chuyển · Chờ xác nhận tiền", "Đã nhận tiền giữ chỗ", "Đã hủy yêu cầu" })
            Assert.Contains(label, html);
        Assert.Contains("Hạn chuyển khoản", html);
        Assert.Contains("Hạn ký hợp đồng", html);
        Assert.Contains("Số tiền đã xác nhận", html);
        Assert.Contains("275.000", html);
        Assert.DoesNotContain("VIEW-OLDEST", html);
        Assert.Equal(5, Regex.Matches(html, "data-dashboard-viewing=").Count);
        var approvedCard = Regex.Match(html, $"<li[^>]*data-dashboard-reservation=\"{data.ApprovedId}\"[^>]*>(.*?)</li>", RegexOptions.Singleline).Value;
        Assert.Contains("Đã duyệt · Chờ chuyển khoản", approvedCard);
        Assert.Contains("Số tiền đã xác nhận: <strong>0 VNĐ</strong>", approvedCard);
        Assert.DoesNotContain("Đã nhận tiền giữ chỗ", approvedCard);
        var terminalCards = Regex.Matches(html, "<li[^>]*data-dashboard-reservation=\"[^\"]+\"[^>]*>.*?</li>", RegexOptions.Singleline)
            .Select(match => match.Value)
            .Where(card => card.Contains("HOLD-DangGiuCho") || card.Contains("HOLD-DaHuy"))
            .ToArray();
        Assert.Equal(2, terminalCards.Length);
        foreach (var card in terminalCards)
        {
            Assert.Contains("Số tiền giữ chỗ: 275.000 VNĐ", card);
            Assert.DoesNotContain("Số tiền cần chuyển", card);
        }
        Assert.Contains("/tai-khoan/yeu-cau-phong", html);
        Assert.Contains($"/tai-khoan/yeu-cau-phong/giu-cho/{data.ApprovedId}/dat-coc", html);
        Assert.DoesNotContain("PRIVATE-OTHER-CUSTOMER", html);
        if (tenant)
        {
            Assert.Contains("Hóa đơn chưa thanh toán", html);
            Assert.Contains("/KhachThue/HoaDon", html);
            Assert.Contains("/KhachThue/HopDong", html);
        }
        else
        {
            Assert.DoesNotContain("Hóa đơn chưa thanh toán", html);
            Assert.DoesNotContain("Tình trạng hợp đồng", html);
            Assert.DoesNotContain("/KhachThue/HoaDon", html);
            Assert.DoesNotContain("/KhachThue/HopDong", html);
            Assert.DoesNotContain("/KhachThue/HoSo", html);
        }

        // A newer rejected request replaces only the oldest reservation in the summary.
        await data.AddRejectedReservationAsync();
        html = WebUtility.HtmlDecode(await client.GetStringAsync("/KhachThue/Dashboard"));
        Assert.Contains("Từ chối yêu cầu", html);
        Assert.DoesNotContain("ROOM-OLDEST-REQUEST", html);
        Assert.Equal(5, Regex.Matches(html, "data-dashboard-reservation=").Count);
    }

    [Fact]
    public async Task GuestDashboardDoesNotCreateProfilesAndBecomesDefaultLanding()
    {
        await using var data = await DashboardData.CreateAsync(factory, false);
        using var client = CreateClient();
        var login = await LoginAsync(client, data.Username);
        Assert.Equal("/KhachThue/Dashboard", login.Headers.Location?.ToString());
        var html = WebUtility.HtmlDecode(await client.GetStringAsync("/KhachThue/Dashboard"));
        Assert.Contains("Bạn chưa có lịch xem phòng", html);
        Assert.Contains("Bạn chưa có yêu cầu giữ chỗ", html);
        Assert.Contains("href=\"/KhachThue/Dashboard\"", html);
        Assert.Equal("/KhachThue/Dashboard", (await client.GetAsync("/")).Headers.Location?.ToString());
        Assert.Equal("/KhachThue/Dashboard", (await client.GetAsync("/QuanLyNhaTro/DangNhap")).Headers.Location?.ToString());
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await db.NguoiDungs.AsNoTracking().SingleAsync(u => u.TenDangNhap == data.Username);
        Assert.Null(user.NguoiThueId);
        Assert.False(await db.KhachVangLais.AnyAsync(g => g.NguoiDungId == user.NguoiDungId));
    }

    [Fact]
    public async Task AnonymousDashboardRequiresLogin()
    {
        using var client = CreateClient();
        var response = await client.GetAsync("/KhachThue/Dashboard");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("DangNhap", response.Headers.Location?.ToString());
    }

    private HttpClient CreateClient() => factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false, HandleCookies = true
    });

    private static async Task<HttpResponseMessage> LoginAsync(HttpClient client, string username)
    {
        var html = await client.GetStringAsync("/QuanLyNhaTro/DangNhap");
        var token = WebUtility.HtmlDecode(Regex.Match(html,
            "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
        var response = await client.PostAsync("/QuanLyNhaTro/DangNhap", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token, ["TenDangNhap"] = username, ["MatKhau"] = "Dashboard123!"
        }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return response;
    }

    private sealed class DashboardData(CustomWebApplicationFactory factory, string username) : IAsyncDisposable
    {
        public string Username { get; } = username;
        public int ApprovedId { get; private set; }
        private readonly List<int> userIds = [];
        private readonly List<int> tenantIds = [];
        private int? branchId;
        private int actorId;
        private int guestId;
        private int otherGuestId;

        public static async Task<DashboardData> CreateAsync(CustomWebApplicationFactory factory, bool tenant)
        {
            var suffix = Guid.NewGuid().ToString("N")[..10];
            var data = new DashboardData(factory, "dashboard_" + suffix);
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var passwords = scope.ServiceProvider.GetRequiredService<IPasswordService>();
            var user = new NguoiDung
            {
                TenDangNhap = data.Username, MatKhauHash = passwords.HashPassword("Dashboard123!"),
                Role = tenant ? Role.KhachThue : Role.KhachVangLai
            };
            if (tenant)
                user.NguoiThue = new NguoiThue { HoVaTen = "Dashboard tenant", CCCD = "D" + suffix,
                    Email = suffix + "@example.com", SoDienThoai = "0912345678" };
            db.NguoiDungs.Add(user);
            await db.SaveChangesAsync();
            data.actorId = user.NguoiDungId;
            data.userIds.Add(user.NguoiDungId);
            if (user.NguoiThueId.HasValue) data.tenantIds.Add(user.NguoiThueId.Value);
            return data;
        }

        public async Task AddRequestsAsync()
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var branch = new ChiNhanh { MaChiNhanh = Guid.NewGuid().ToString("N")[..8],
                TenChiNhanh = "Dashboard tests", DiaChi = "A", MoTa = "A", SoDienThoai = "0912345678" };
            var guest = new KhachVangLai { NguoiDungId = actorId, HoTen = "Customer", SoDienThoai = "0912345678" };
            var otherUser = new NguoiDung { TenDangNhap = Username + "_other", MatKhauHash = "unused", Role = Role.KhachVangLai };
            var other = new KhachVangLai { NguoiDung = otherUser, HoTen = "Other", SoDienThoai = "0912345678" };
            db.AddRange(branch, guest, other);
            await db.SaveChangesAsync();
            branchId = branch.ChiNhanhId;
            guestId = guest.KhachVangLaiId;
            otherGuestId = other.KhachVangLaiId;
            userIds.Add(otherUser.NguoiDungId);
            var now = DateTime.UtcNow;
            foreach (var state in new[] { TrangThaiYeuCauXemPhong.ChoNhanVienXuLy, TrangThaiYeuCauXemPhong.DaXacNhanLich,
                TrangThaiYeuCauXemPhong.TuChoi, TrangThaiYeuCauXemPhong.DaHuy })
                db.YeuCauXemPhongs.Add(new YeuCauXemPhong { KhachVangLaiId = guestId, PhongTro = Room("VIEW-" + state),
                    HoTen = "Customer", SoDienThoai = "0912345678", TrangThai = state,
                    ThoiGianMongMuon = now.AddDays(1), ThoiGianXacNhan = state == TrangThaiYeuCauXemPhong.DaXacNhanLich ? now.AddDays(2) : null });
            db.YeuCauXemPhongs.Add(new YeuCauXemPhong { KhachVangLaiId = guestId, PhongTro = Room("VIEW-OLDEST"),
                HoTen = "Customer", SoDienThoai = "0912345678", ThoiGianMongMuon = now.AddDays(1), NgayTao = now.AddDays(-5) });
            db.YeuCauXemPhongs.Add(new YeuCauXemPhong { KhachVangLaiId = guestId, PhongTro = Room("VIEW-RECENT"),
                HoTen = "Customer", SoDienThoai = "0912345678", ThoiGianMongMuon = now.AddDays(1), NgayTao = now.AddDays(-1) });
            var states = new[] { TrangThaiYeuCauGiuCho.MoiTao, TrangThaiYeuCauGiuCho.ChoThanhToan,
                TrangThaiYeuCauGiuCho.ChoXacNhanTien, TrangThaiYeuCauGiuCho.DangGiuCho, TrangThaiYeuCauGiuCho.DaHuy };
            foreach (var state in states)
            {
                var hold = new YeuCauGiuCho { KhachVangLaiId = guestId,
                    PhongTro = Room(state == TrangThaiYeuCauGiuCho.MoiTao ? "ROOM-OLDEST-REQUEST" : "HOLD-" + state),
                    TrangThai = state, NgayTao = now.AddMinutes((int)state), SoTienGiuCho = 275000m,
                    HanThanhToan = now.AddDays(1), HanKyHopDong = now.AddDays(2) };
                if (state == TrangThaiYeuCauGiuCho.DangGiuCho)
                    hold.YeuCauThanhToanGiuChos.Add(new YeuCauThanhToanGiuCho { NguoiTaoId = actorId,
                        MaYeuCau = "PAY-" + Username, NoiDungChuyenKhoan = "Dashboard", SoTien = 275000m,
                        HanThanhToan = now.AddDays(1), TrangThai = TrangThaiYeuCauThanhToan.DaHoanTat,
                        GiaoDichGiuChos = [new GiaoDichGiuCho { NguoiXacNhanId = actorId, MaGiaoDich = "TX-" + Username,
                            SoTienThucNhan = 275000m, NgayThucNhan = now }] });
                db.YeuCauGiuChos.Add(hold);
                await db.SaveChangesAsync();
                if (state == TrangThaiYeuCauGiuCho.ChoThanhToan) ApprovedId = hold.YeuCauGiuChoId;
            }
            var privateRoom = Room("PRIVATE-OTHER-CUSTOMER");
            db.YeuCauXemPhongs.Add(new YeuCauXemPhong { KhachVangLaiId = otherGuestId, PhongTro = privateRoom,
                HoTen = "Other", SoDienThoai = "0912345678", ThoiGianMongMuon = now.AddDays(1) });
            db.YeuCauGiuChos.Add(new YeuCauGiuCho { KhachVangLaiId = otherGuestId, PhongTro = privateRoom });
            await db.SaveChangesAsync();
        }

        public async Task AddRejectedReservationAsync()
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.YeuCauGiuChos.Add(new YeuCauGiuCho { KhachVangLaiId = guestId, PhongTro = Room("LATEST-REJECTED"),
                TrangThai = TrangThaiYeuCauGiuCho.TuChoi, NgayTao = DateTime.UtcNow.AddHours(1) });
            await db.SaveChangesAsync();
        }

        private PhongTro Room(string number) => new() { ChiNhanhId = branchId!.Value,
            SoPhong = number, GiaThue = 1000000m, DienTich = 20, MoTa = "Dashboard test", TrangThai = TrangThaiPhong.Trong };

        public async ValueTask DisposeAsync()
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var holds = db.YeuCauGiuChos.Where(h => userIds.Contains(h.KhachVangLai.NguoiDungId));
            await db.GiaoDichGiuChos.Where(g => holds.Any(h => h.YeuCauGiuChoId == g.YeuCauThanhToanGiuCho.YeuCauGiuChoId)).ExecuteDeleteAsync();
            await db.YeuCauThanhToanGiuChos.Where(p => holds.Any(h => h.YeuCauGiuChoId == p.YeuCauGiuChoId)).ExecuteDeleteAsync();
            await holds.ExecuteDeleteAsync();
            await db.YeuCauXemPhongs.Where(v => v.KhachVangLai != null && userIds.Contains(v.KhachVangLai.NguoiDungId)).ExecuteDeleteAsync();
            await db.KhachVangLais.Where(g => userIds.Contains(g.NguoiDungId)).ExecuteDeleteAsync();
            await db.NguoiDungs.Where(u => userIds.Contains(u.NguoiDungId)).ExecuteDeleteAsync();
            await db.NguoiThues.Where(t => tenantIds.Contains(t.NguoiThueId)).ExecuteDeleteAsync();
            if (branchId.HasValue)
            {
                await db.PhongTros.Where(p => p.ChiNhanhId == branchId).ExecuteDeleteAsync();
                await db.ChiNhanhs.Where(b => b.ChiNhanhId == branchId).ExecuteDeleteAsync();
            }
        }
    }
}
