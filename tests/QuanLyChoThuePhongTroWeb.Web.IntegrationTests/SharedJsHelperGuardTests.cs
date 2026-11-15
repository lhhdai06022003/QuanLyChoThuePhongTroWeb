using System.Text.RegularExpressions;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Web.IntegrationTests
{
    // Hàm escape HTML và định dạng tiền của cổng quản lý và cổng khách thuê nằm một chỗ ở wwwroot/js/site.js
    // (docs/features/toi-uu-hieu-nang, bước 21). Chỉ đọc file nguồn, không cần DB hay server.
    public class SharedJsHelperGuardTests
    {
        [Fact]
        public void SiteJs_KhaiBaoHamEscapeVaDinhDangTien()
        {
            var js = ViewSource.Read("wwwroot", "js", "site.js");

            var escape = Regex.Match(js, @"window\.escapeHtml = function \(value\) \{.*?\n    \};", RegexOptions.Singleline).Value;
            Assert.False(string.IsNullOrEmpty(escape), "site.js thiếu window.escapeHtml");
            // Đủ năm ký tự nguy hiểm; null và undefined thành chuỗi rỗng, số 0 vẫn in ra.
            foreach (var needle in new[] { "&amp;", "&lt;", "&gt;", "&quot;", "&#039;" })
            {
                Assert.Contains(needle, escape);
            }
            Assert.Contains("value === null || value === undefined ? '' : value", escape);

            Assert.Contains("window.formatVND = function (n)", js);
            Assert.Contains("new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' })", js);
        }

        [Theory]
        [InlineData("Views", "Shared", "_Layout.cshtml")]
        [InlineData("Areas", "KhachThue", "Views", "Shared", "_LayoutKhachThue.cshtml")]
        public void HaiLayout_NapSiteJsTruocScriptCuaMan(params string[] path)
        {
            var layout = ViewSource.Read(path);
            var siteJs = layout.IndexOf("<script src=\"~/js/site.js\" asp-append-version=\"true\"></script>", StringComparison.Ordinal);
            Assert.True(siteJs > 0, "Layout phải nạp ~/js/site.js");

            // Script của từng màn (section Scripts) và thongbao.js gọi escapeHtml của site.js nên phải đứng sau.
            Assert.True(siteJs < layout.IndexOf("RenderSectionAsync(\"Scripts\"", StringComparison.Ordinal));
            Assert.True(siteJs < layout.IndexOf("~/js/thongbao.js", StringComparison.Ordinal));
        }

        [Fact]
        public void ThongBaoJs_DungHamEscapeCuaSiteJs()
        {
            var js = ViewSource.Read("wwwroot", "js", "thongbao.js");
            Assert.DoesNotMatch(@"function\s+escapeHtml\s*\(", js);
            Assert.Contains("escapeHtml(tb.tieuDe)", js);
            Assert.Contains("escapeHtml(tb.noiDung)", js);
        }

        // File JS của hai cổng và view trong Areas không khai báo lại hàm đã có ở site.js.
        // Ngoại lệ: ai-chat-widget.js tự đóng gói, giữ cách escape riêng (không đổi null thành chuỗi rỗng);
        // file của cổng khách vãng lai (guest-*, khach-vang-lai-*) chạy trên layout không nạp site.js.
        [Theory]
        [InlineData("escapeHtml")]
        [InlineData("esc")]
        public void KhongFileNaoKhaiBaoLaiHamDungChung(string tenHam)
        {
            var root = ViewSource.WebRoot();
            var files = Directory.GetFiles(Path.Combine(root, "wwwroot", "js"), "*.js")
                .Where(f => !Regex.IsMatch(Path.GetFileName(f), @"^(site\.js|ai-chat-widget\.js|guest-.*|khach-vang-lai-.*)$"))
                .Concat(Directory.GetFiles(Path.Combine(root, "Areas"), "*.cshtml", SearchOption.AllDirectories))
                .ToList();
            Assert.True(files.Count > 30, "Không tìm thấy đủ file JS và view để soát");

            var khaiBao = new Regex($@"function\s+{Regex.Escape(tenHam)}\s*\(");
            var viPham = files.Where(f => khaiBao.IsMatch(File.ReadAllText(f)))
                .Select(f => Path.GetRelativePath(root, f))
                .ToList();

            Assert.True(viPham.Count == 0, $"Khai báo lại {tenHam} (dùng hàm trong site.js): " + string.Join(", ", viPham));
        }

        // Màn Lịch sử thanh toán trước đây để trống ô số lượng khi số lượng bằng 0; giữ nguyên cách hiện đó.
        [Fact]
        public void LichSuThanhToan_GiuCachHienSoLuongBangKhong()
        {
            var js = ViewSource.Read("wwwroot", "js", "lich-su-thanh-toan.js");
            Assert.Contains("var esc = window.escapeHtml;", js);
            Assert.Contains("esc(ct.soLuong || '')", js);
        }
    }
}
