using System.Text.RegularExpressions;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Web.IntegrationTests
{
    // Chart.js, Select2, flatpickr không còn nạp ở layout chung mà chỉ ở trang dùng (docs/features/toi-uu-hieu-nang).
    // Chặn trang mới gọi thư viện mà quên khai báo partial, vì lỗi đó chỉ lộ ra ở trình duyệt ("flatpickr is not defined").
    public class LayoutLibraryStaticGuardTests
    {
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

        private static readonly (string Name, Regex Usage, string Partial, string? Css)[] Libraries =
        {
            ("flatpickr", new Regex(@"\bflatpickr\("), "_FlatpickrJs", "flatpickr/4.6.13/flatpickr.min.css"),
            ("Select2", new Regex(@"\.select2\(\s*\{"), "_Select2Js", "select2/4.0.13/css/select2.min.css"),
            ("Chart.js", new Regex(@"new Chart\("), "_ChartJs", null)
        };

        // Nội dung view kèm các file ~/js/*.js mà view nạp.
        private static string ViewWithScripts(string root, string viewPath)
        {
            var content = File.ReadAllText(viewPath);
            foreach (Match m in Regex.Matches(content, "src=\"~/js/([^\"]+\\.js)\""))
            {
                var js = Path.Combine(root, "wwwroot", "js", m.Groups[1].Value);
                if (File.Exists(js))
                {
                    content += "\n" + File.ReadAllText(js);
                }
            }
            return content;
        }

        [Fact]
        public void ManagerLayout_DoesNotLoadPageOnlyLibraries()
        {
            var layout = File.ReadAllText(Path.Combine(WebRoot(), "Views", "Shared", "_Layout.cshtml"));

            Assert.DoesNotContain("flatpickr.min.js", layout);
            Assert.DoesNotContain("flatpickr.min.css", layout);
            Assert.DoesNotContain("select2.full.min.js", layout);
            Assert.DoesNotContain("select2.min.css", layout);
            Assert.DoesNotContain("npm/chart.js", layout);
            Assert.Contains("RenderSectionAsync(\"ThuVienScripts\"", layout);
        }

        [Fact]
        public void PagesUsingALibrary_DeclareItsPartialAndCss()
        {
            var root = WebRoot();
            var views = Directory.EnumerateFiles(Path.Combine(root, "Areas", "QuanLyNhaTro", "Views"), "*.cshtml", SearchOption.AllDirectories)
                .Concat(Directory.EnumerateFiles(Path.Combine(root, "Views", "Home"), "*.cshtml", SearchOption.AllDirectories))
                .Where(f => !Path.GetFileName(f).StartsWith("_"));

            var missing = new List<string>();
            foreach (var view in views)
            {
                var content = ViewWithScripts(root, view);
                foreach (var lib in Libraries)
                {
                    if (!lib.Usage.IsMatch(content))
                    {
                        continue;
                    }

                    var name = Path.GetRelativePath(root, view);
                    if (!content.Contains($"<partial name=\"{lib.Partial}\""))
                    {
                        missing.Add($"{name}: dùng {lib.Name} nhưng thiếu <partial name=\"{lib.Partial}\" /> trong @section ThuVienScripts");
                    }
                    if (lib.Css != null && !content.Contains(lib.Css))
                    {
                        missing.Add($"{name}: dùng {lib.Name} nhưng thiếu CSS {lib.Css} trong @section Styles");
                    }
                }
            }

            Assert.True(missing.Count == 0, string.Join("\n", missing));
        }
    }
}
