using System.Text.RegularExpressions;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Web.IntegrationTests
{
    // wwwroot/Theme từng chứa cả gói Tabler (48 MB, 1.966 file) dù trang chỉ nạp 4 file; wwwroot/lib/bootstrap (7 MB) không trang nào
    // dùng (docs/features/toi-uu-hieu-nang, bước 17). Thư mục chỉ giữ file được tham chiếu, kèm source map và font của chúng.
    public class WwwrootVendorFilesTests
    {
        private static readonly string[] ThemeFiles =
        {
            // Logo trường: tài sản riêng của dự án (không thuộc gói Tabler), giữ lại dù chưa trang nào nạp.
            "images/logo-hcmute.png",
            "tabler/dist/css/tabler.min.css",
            "tabler/dist/css/tabler.min.css.map",
            "tabler/dist/css/tabler-vendors.min.css",
            "tabler/dist/css/tabler-vendors.min.css.map",
            "tabler/dist/css/tabler-icons.min.css",
            "tabler/dist/css/fonts/tabler-icons.eot",
            "tabler/dist/css/fonts/tabler-icons.ttf",
            "tabler/dist/css/fonts/tabler-icons.woff",
            "tabler/dist/css/fonts/tabler-icons.woff2",
            "tabler/dist/js/tabler.min.js",
            "tabler/dist/js/tabler.min.js.map"
        };

        private static string Wwwroot() => Path.Combine(ViewSource.WebRoot(), "wwwroot");

        [Fact]
        public void ThemeFolder_HoldsExactlyTheReferencedFiles()
        {
            var theme = Path.Combine(Wwwroot(), "Theme");
            var actual = Directory.EnumerateFiles(theme, "*", SearchOption.AllDirectories)
                .Select(f => Path.GetRelativePath(theme, f).Replace('\\', '/'))
                .OrderBy(f => f, StringComparer.Ordinal)
                .ToArray();

            Assert.Equal(ThemeFiles.OrderBy(f => f, StringComparer.Ordinal).ToArray(), actual);
        }

        [Fact]
        public void UnusedBootstrapCopy_IsNotShipped()
        {
            Assert.False(Directory.Exists(Path.Combine(Wwwroot(), "lib", "bootstrap")), "wwwroot/lib/bootstrap không trang nào nạp (Tabler đã gồm Bootstrap)");
        }

        // Font icon nạp qua url() tương đối trong CSS: thiếu file thì mọi icon thành ô vuông.
        [Fact]
        public void IconCss_FontUrls_PointAtExistingFiles()
        {
            var cssDir = Path.Combine(Wwwroot(), "Theme", "tabler", "dist", "css");
            var css = File.ReadAllText(Path.Combine(cssDir, "tabler-icons.min.css"));
            var urls = Regex.Matches(css, @"url\((['""]?)(?!data:)([^)'""?#]+)[^)]*\)").Select(m => m.Groups[2].Value).Distinct().ToList();

            Assert.NotEmpty(urls);
            Assert.All(urls, url => Assert.True(File.Exists(Path.Combine(cssDir, url.Replace('/', Path.DirectorySeparatorChar))), $"Thiếu file {url}"));
        }

        // Mọi file tĩnh cục bộ mà view nạp bằng ~/Theme hoặc ~/lib phải có thật.
        [Fact]
        public void EveryThemeOrLibFile_ReferencedByAView_Exists()
        {
            var root = ViewSource.WebRoot();
            var views = Directory.EnumerateFiles(Path.Combine(root, "Areas"), "*.cshtml", SearchOption.AllDirectories)
                .Concat(Directory.EnumerateFiles(Path.Combine(root, "Views"), "*.cshtml", SearchOption.AllDirectories));

            var missing = new List<string>();
            var found = 0;
            foreach (var view in views)
            {
                foreach (Match m in Regex.Matches(File.ReadAllText(view), "(?:href|src)=\"~/((?:Theme|lib)/[^\"?]+)\""))
                {
                    found++;
                    if (!File.Exists(Path.Combine(Wwwroot(), m.Groups[1].Value.Replace('/', Path.DirectorySeparatorChar))))
                    {
                        missing.Add($"{Path.GetRelativePath(root, view)}: {m.Groups[1].Value}");
                    }
                }
            }

            Assert.True(found >= 10, $"Chỉ tìm thấy {found} tham chiếu ~/Theme, ~/lib");
            Assert.True(missing.Count == 0, string.Join("\n", missing));
        }
    }
}
