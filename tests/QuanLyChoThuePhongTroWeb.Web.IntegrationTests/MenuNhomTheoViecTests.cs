using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Web.IntegrationTests.Fixtures;
using Xunit;
using static QuanLyChoThuePhongTroWeb.Web.IntegrationTests.BranchScopeKit;

namespace QuanLyChoThuePhongTroWeb.Web.IntegrationTests
{
    // Menu quản lý nhóm theo việc: đúng tập đường dẫn từng vai trò, có nhãn nhóm, không còn nhóm xổ xuống, đúng một mục đang mở.
    [Collection(WebTestCollection.Name)]
    public class MenuNhomTheoViecTests
    {
        private const string Dashboard = "/QuanLyNhaTro/Dashboard";

        private static readonly string[] NhanVien =
        {
            "/Home/Index", "/QuanLyNhaTro/ViecCanLam",
            "/PhongTros/QuanLyPhongTro", "/QuanLyNhaTro/QuanLyNguoiThue", "/QuanLyNhaTro/QuanLyHopDong",
            "/QuanLyNhaTro/ChotDienNuoc", "/QuanLyNhaTro/QuanLyHoaDon", "/QuanLyNhaTro/DoiChieuThanhToan", "/QuanLyNhaTro/LichSuThanhToan",
            "/QuanLyNhaTro/TinPhong", "/QuanLyNhaTro/LichXemPhong", "/QuanLyNhaTro/GiuCho",
            "/QuanLyNhaTro/YeuCauSuCo", "/QuanLyNhaTro/BaoCao",
            "/QuanLyNhaTro/QuanLyDichVu", "/QuanLyNhaTro/DieuKhoanMau"
        };

        private static readonly string[] ChiAdmin =
        {
            "/QuanLyNhaTro/GiaoDichSePay", "/ChiNhanhs/QuanLyChiNhanh",
            "/QuanLyNhaTro/QuanLyTaiKhoanDangNhap", "/QuanLyNhaTro/PhanCongChiNhanh"
        };

        private readonly CustomWebApplicationFactory _factory;

        public MenuNhomTheoViecTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
        }

        private static async Task<string> HtmlAsync(HttpClient client, string url)
        {
            var res = await client.GetAsync(url);
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            return WebUtility.HtmlDecode(await res.Content.ReadAsStringAsync());
        }

        // Đoạn menu: từ id="sidebar-menu" tới thẻ đóng aside của thanh bên.
        private static string DoanMenu(string html)
        {
            var batDau = html.IndexOf("id=\"sidebar-menu\"", StringComparison.Ordinal);
            Assert.True(batDau >= 0, "Trang không có id=\"sidebar-menu\"");
            var ketThuc = html.IndexOf("</aside>", batDau, StringComparison.Ordinal);
            Assert.True(ketThuc > batDau, "Không thấy thẻ đóng aside của thanh bên");
            return html.Substring(batDau, ketThuc - batDau);
        }

        private static IEnumerable<string> DuongDan(string menu) =>
            Regex.Matches(menu, "href=\"([^\"]+)\"").Select(m => m.Groups[1].Value).Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal);

        [Fact]
        public async Task Menu_NhanVien_ThayDung16Muc_KhongCoMucChiAdmin()
        {
            var w = await SeedAsync(_factory);
            var (staff, _) = await LoginAsync(_factory, w.Staff);

            var menu = DoanMenu(await HtmlAsync(staff, Dashboard));

            Assert.Equal(NhanVien.OrderBy(x => x, StringComparer.Ordinal), DuongDan(menu));
        }

        [Fact]
        public async Task Menu_Admin_ThayDu20Muc()
        {
            var w = await SeedAsync(_factory);
            var (admin, _) = await LoginAsync(_factory, w.Admin);

            var menu = DoanMenu(await HtmlAsync(admin, Dashboard));

            Assert.Equal(NhanVien.Concat(ChiAdmin).OrderBy(x => x, StringComparer.Ordinal), DuongDan(menu));
        }

        [Fact]
        public async Task Menu_HienNhanNhom_VaKhongConNhomXoXuong()
        {
            var w = await SeedAsync(_factory);
            var (admin, _) = await LoginAsync(_factory, w.Admin);

            var menu = DoanMenu(await HtmlAsync(admin, Dashboard));

            foreach (var nhan in new[] { "Phòng và khách", "Thu tiền", "Khách mới", "Vận hành", "Thiết lập" })
                Assert.Contains(nhan, menu);
            Assert.DoesNotContain("dropdown-toggle", menu);
            Assert.DoesNotContain("Quản lý nhà trọ", menu);
            Assert.DoesNotContain("Quản lý người dùng", menu);
            Assert.Contains("id=\"badge-viec-can-lam\"", menu);
        }

        [Fact]
        public async Task Menu_MucDangMo_CoLopActive()
        {
            var w = await SeedAsync(_factory);
            var (admin, _) = await LoginAsync(_factory, w.Admin);

            var menu = DoanMenu(await HtmlAsync(admin, "/QuanLyNhaTro/QuanLyHoaDon"));

            var the = Regex.Match(menu, "<a\\b[^>]*\\bhref=\"/QuanLyNhaTro/QuanLyHoaDon\"[^>]*>");
            Assert.True(the.Success, "Menu không có liên kết Hóa đơn");
            Assert.Contains("nav-link active", the.Value);
            Assert.Contains("aria-current=\"page\"", the.Value);
            Assert.Single(Regex.Matches(menu, "nav-link active"));
        }
    }
}
