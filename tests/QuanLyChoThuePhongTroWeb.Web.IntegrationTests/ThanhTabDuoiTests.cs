using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using QuanLyChoThuePhongTroWeb.Application.Common.Enums;
using QuanLyChoThuePhongTroWeb.Application.Features.NguoiDungs.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.NguoiDungs.Services;
using QuanLyChoThuePhongTroWeb.Web.IntegrationTests.Fixtures;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Web.IntegrationTests
{
    // Thanh tab dưới dùng chung (.ts-bottom-nav): quản lý 4 liên kết + nút Thêm, khách thuê 5 mục, khách vãng lai không có.
    [Collection(WebTestCollection.Name)]
    public class ThanhTabDuoiTests
    {
        private static readonly string[] MucQuanLy =
        {
            "/QuanLyNhaTro/ViecCanLam", "/PhongTros/QuanLyPhongTro", "/QuanLyNhaTro/ChotDienNuoc", "/QuanLyNhaTro/QuanLyHoaDon"
        };

        private static readonly string[] MucKhachThue =
        {
            "/KhachThue/Dashboard", "/KhachThue/HoaDon", "/KhachThue/ChiSoDienNuoc", "/KhachThue/SuCo", "/KhachThue/HoSo"
        };

        private readonly CustomWebApplicationFactory _factory;

        public ThanhTabDuoiTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
        }

        private static async Task<string> HtmlAsync(HttpClient client, string url)
        {
            var res = await client.GetAsync(url);
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            return WebUtility.HtmlDecode(await res.Content.ReadAsStringAsync());
        }

        // Đoạn thanh tab: từ thẻ mở nav tới </nav> kế tiếp. _Layout.cshtml có CSS cô lập nên Razor chèn thuộc tính
        // dạng b-xxxxxxxxxx ngay sau tên thẻ (<nav b-xxx class="...">): dò bằng regex thay vì chuỗi cố định.
        private static string DoanThanhTab(string html, string moDau)
        {
            var lop = moDau.Replace("<nav class=\"", string.Empty);
            var m = Regex.Match(html, "<nav(?:\\s+b-[a-z0-9]+)?\\s+class=\"" + Regex.Escape(lop));
            var batDau = m.Success ? m.Index : -1;
            Assert.True(batDau >= 0, $"Trang không có thanh tab ({moDau})");
            var ketThuc = html.IndexOf("</nav>", batDau, StringComparison.Ordinal);
            Assert.True(ketThuc > batDau, "Không thấy thẻ đóng nav của thanh tab");
            return html.Substring(batDau, ketThuc - batDau);
        }

        private static string[] DanhSachHref(string doan) =>
            Regex.Matches(doan, "href=\"([^\"]+)\"").Select(m => m.Groups[1].Value).ToArray();

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ThanhTab_AdminVaNhanVien_Co4LienKetVaNutThem(bool dungAdmin)
        {
            var w = await BranchScopeKit.SeedAsync(_factory);
            var (client, _) = await BranchScopeKit.LoginAsync(_factory, dungAdmin ? w.Admin : w.Staff);

            var html = await HtmlAsync(client, "/QuanLyNhaTro/Dashboard");
            var nav = DoanThanhTab(html, "<nav class=\"ts-bottom-nav");

            Assert.Equal(MucQuanLy, DanhSachHref(nav));
            Assert.Contains("js-mo-menu-day-du", nav);
            Assert.Contains("Thêm", nav);
            Assert.Matches("<body[^>]*ts-co-thanh-tab", html);
        }

        [Fact]
        public async Task ThanhTab_KhachThue_NamMuc_MucCuoiLaToi()
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

            var html = await HtmlAsync(client, "/KhachThue/Dashboard");
            var nav = DoanThanhTab(html, "<nav class=\"tenant-bottom-nav ts-bottom-nav");

            Assert.Equal(MucKhachThue, DanhSachHref(nav));
            Assert.Contains("Tôi", nav);
            Assert.Matches("<body[^>]*ts-cong-khach-thue", html);
            Assert.Matches("<body[^>]*ts-co-thanh-tab", html);
        }

        [Fact]
        public void CongKhachVangLai_KhongCoThanhTabMoi()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "QuanLyChoThuePhongTroWeb.sln")))
            {
                dir = dir.Parent;
            }
            Assert.NotNull(dir);
            var html = File.ReadAllText(Path.Combine(dir!.FullName, "src", "QuanLyChoThuePhongTroWeb.Web", "Views", "Shared", "_PublicRoomLayout.cshtml"));

            Assert.DoesNotContain("ts-bottom-nav", html);
            Assert.DoesNotContain("ts-co-thanh-tab", html);
        }
    }
}
