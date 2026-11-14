using System.Text.RegularExpressions;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Web.IntegrationTests
{
    // File tĩnh cục bộ chỉ được cache lâu khi URL có ?v= (Helpers/StaticAssetDelivery.cs). Layout và các trang tự dựng
    // khung HTML của cổng quản lý, khách thuê phải gắn asp-append-version cho mọi CSS/JS cục bộ
    // (docs/features/toi-uu-hieu-nang, bước 13); thiếu thì tabler.min.css, jquery... không có Cache-Control.
    public class LayoutAssetVersionGuardTests
    {
        private static readonly string[] Files =
        {
            Path.Combine("Views", "Shared", "_Layout.cshtml"),
            Path.Combine("Areas", "KhachThue", "Views", "Shared", "_LayoutKhachThue.cshtml"),
            Path.Combine("Areas", "QuanLyNhaTro", "Views", "NguoiDung", "DangNhap.cshtml"),
            Path.Combine("Areas", "QuanLyNhaTro", "Views", "HopDong", "Print.cshtml")
        };

        private static readonly Regex LocalAsset = new("<(?:link|script)\\b[^>]*\\b(?:href|src)=\"~/(?:Theme|lib|css|js)/[^\"]+\\.(?:css|js)\"[^>]*>");

        private static string WebRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "QuanLyChoThuePhongTroWeb.sln")))
            {
                dir = dir.Parent;
            }
            Assert.NotNull(dir);
            return Path.Combine(dir!.FullName, "src", "QuanLyChoThuePhongTroWeb.Web");
        }

        [Fact]
        public void LocalCssAndJs_InLayouts_CarryAppendVersion()
        {
            var root = WebRoot();
            var missing = new List<string>();
            var found = 0;
            foreach (var file in Files)
            {
                var content = File.ReadAllText(Path.Combine(root, file));
                foreach (Match tag in LocalAsset.Matches(content))
                {
                    found++;
                    if (!tag.Value.Contains("asp-append-version=\"true\""))
                    {
                        missing.Add($"{file}: {tag.Value}");
                    }
                }
            }

            Assert.True(found >= 12, $"Chỉ tìm thấy {found} thẻ CSS/JS cục bộ: biểu thức dò có thể đã lệch với cách viết thẻ.");
            Assert.True(missing.Count == 0, "Thiếu asp-append-version:\n" + string.Join("\n", missing));
        }
    }
}
