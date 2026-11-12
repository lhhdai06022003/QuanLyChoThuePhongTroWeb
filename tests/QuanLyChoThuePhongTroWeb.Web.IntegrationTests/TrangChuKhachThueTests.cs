using System;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using QuanLyChoThuePhongTroWeb.Application.Common.Enums;
using QuanLyChoThuePhongTroWeb.Application.Features.NguoiDungs.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.NguoiDungs.Services;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence;
using QuanLyChoThuePhongTroWeb.Web.Helpers;
using QuanLyChoThuePhongTroWeb.Web.IntegrationTests.Fixtures;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Web.IntegrationTests
{
    // Trang chủ khách thuê: thẻ Cần thanh toán (số tiền, số hóa đơn, hạn trả theo ngày Việt Nam, nút thanh toán) và ba lối tắt.
    [Collection(WebTestCollection.Name)]
    public class TrangChuKhachThueTests
    {
        private readonly CustomWebApplicationFactory _factory;

        public TrangChuKhachThueTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
        }

        private async Task<(HttpClient Client, BranchScopeWorld World)> TaoKhachThueAsync()
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
            return (client, w);
        }

        private async Task ThemHoaDonAsync(int hopDongId, decimal tong, DateTime? hanUtc)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.HoaDons.Add(new HoaDon
            {
                MaHoaDon = $"TC{Guid.NewGuid():N}"[..20],
                HopDongId = hopDongId,
                Thang = 1,
                Nam = 2031,
                TongTien = tong,
                TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaGui,
                TrangThaiHoaDon = TrangThaiHoaDon.ChuaThanhToan,
                NgayGui = DateTime.UtcNow.AddDays(-10),
                HanThanhToan = hanUtc
            });
            await db.SaveChangesAsync();
        }

        private static async Task<string> HtmlAsync(HttpClient client)
        {
            var res = await client.GetAsync("/KhachThue/Dashboard");
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            return WebUtility.HtmlDecode(await res.Content.ReadAsStringAsync());
        }

        // Đổi một thời điểm theo giờ Việt Nam (UTC+7) sang UTC.
        private static DateTime VnSangUtc(DateTime gioVn) => DateTime.SpecifyKind(gioVn.AddHours(-7), DateTimeKind.Utc);

        [Fact]
        public async Task ConNo_HienSoTien_HanDungNgayVietNam_NutThanhToan()
        {
            var (client, w) = await TaoKhachThueAsync();
            var homNayVn = DateTime.UtcNow.AddHours(7).Date;
            var ngayHan = homNayVn.AddDays(5);
            // 00:30 giờ Việt Nam: ngày UTC lệch một ngày so với ngày Việt Nam.
            await ThemHoaDonAsync(w.ContractA, 1_234_000m, VnSangUtc(ngayHan.AddMinutes(30)));

            var html = await HtmlAsync(client);

            Assert.Contains(VndFormat.Money(1_234_000m), html);
            Assert.Contains("Cần thanh toán", html);
            Assert.Contains("1 hóa đơn", html);
            Assert.Contains($"Hạn {ngayHan.ToString("dd/MM", CultureInfo.InvariantCulture)}, còn 5 ngày", html);
            Assert.Contains("Thanh toán bằng mã QR", html);
            Assert.Contains("href=\"/KhachThue/HoaDon\"", html);
            Assert.Contains("Hóa đơn chưa thanh toán", html);
            Assert.DoesNotContain("Bạn đã thanh toán đủ", html);
        }

        [Fact]
        public async Task QuaHan_HienSoNgayQuaHan()
        {
            var (client, w) = await TaoKhachThueAsync();
            var homNayVn = DateTime.UtcNow.AddHours(7).Date;
            await ThemHoaDonAsync(w.ContractA, 500_000m, VnSangUtc(homNayVn.AddDays(-3).AddHours(12)));

            var html = await HtmlAsync(client);

            Assert.Contains("Quá hạn 3 ngày", html);
            Assert.Contains("tenant-han is-bad", html);
            Assert.DoesNotContain("tenant-han is-wait", html);
        }

        [Fact]
        public async Task KhongNo_HienDaThanhToanDu()
        {
            var (client, _) = await TaoKhachThueAsync();

            var html = await HtmlAsync(client);

            Assert.Contains("Bạn đã thanh toán đủ", html);
            Assert.Contains("Xem hóa đơn", html);
            Assert.Contains("Hóa đơn chưa thanh toán", html);
            Assert.Contains("href=\"/KhachThue/ChiSoDienNuoc\"", html);
            Assert.Contains("href=\"/KhachThue/SuCo\"", html);
            Assert.Contains("href=\"/KhachThue/HopDong\"", html);
            Assert.DoesNotContain("Thanh toán bằng mã QR", html);
            // Layout vẫn nạp tenant-bento.css nên chỉ kiểm các lớp của lưới cũ, không kiểm chữ "bento" trần.
            Assert.DoesNotContain("bento-grid", html);
            Assert.DoesNotContain("bento-card", html);
        }

        [Fact]
        public void View_KhongConBento()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "QuanLyChoThuePhongTroWeb.sln")))
            {
                dir = dir.Parent;
            }
            Assert.NotNull(dir);
            var web = Path.Combine(dir!.FullName, "src", "QuanLyChoThuePhongTroWeb.Web");
            var view = File.ReadAllText(Path.Combine(web, "Areas", "KhachThue", "Views", "Dashboard", "Index.cshtml"));
            var css = File.ReadAllText(Path.Combine(web, "wwwroot", "css", "tenant-bento.css"));

            Assert.DoesNotContain("bento", view);
            Assert.DoesNotContain("ToString(\"N0\"", view);
            Assert.DoesNotContain("text-white", view);
            Assert.DoesNotContain(".bento-", css);
        }
    }
}
