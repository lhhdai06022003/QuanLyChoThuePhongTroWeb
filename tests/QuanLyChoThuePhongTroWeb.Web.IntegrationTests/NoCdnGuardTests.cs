using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Web.IntegrationTests.Fixtures;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Web.IntegrationTests
{
    // Cổng quản lý và cổng khách thuê nạp thư viện (DataTables, SweetAlert2, SignalR, Font Awesome, flatpickr, Select2, Chart.js,
    // font Be Vietnam Pro) từ wwwroot/lib, không từ CDN (docs/features/toi-uu-hieu-nang, bước 20): trang chạy được khi không có
    // Internet, phiên bản được ghim, file có ?v= nên cache một năm.
    [Collection(WebTestCollection.Name)]
    public class NoCdnGuardTests
    {
        private readonly CustomWebApplicationFactory _factory;

        public NoCdnGuardTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
        }

        private static readonly Regex ExternalAsset = new("<(?:link|script)\\b[^>]*\\b(?:href|src)=\"https?://[^\"]+\"[^>]*>");

        // Cổng khách vãng lai do người khác phụ trách, chưa chuyển: _PublicRoomLayout và các view trong Views/Phong.
        private static IEnumerable<string> ManagerAndTenantViews()
        {
            var root = ViewSource.WebRoot();
            return Directory.EnumerateFiles(Path.Combine(root, "Areas"), "*.cshtml", SearchOption.AllDirectories)
                .Concat(Directory.EnumerateFiles(Path.Combine(root, "Views", "Shared"), "*.cshtml", SearchOption.AllDirectories)
                    .Where(f => !Path.GetFileName(f).StartsWith("_PublicRoom", StringComparison.Ordinal)));
        }

        [Fact]
        public void ManagerAndTenantViews_LoadNoStylesheetOrScriptFromAnotherHost()
        {
            var root = ViewSource.WebRoot();
            var offenders = new List<string>();
            foreach (var view in ManagerAndTenantViews())
            {
                foreach (Match tag in ExternalAsset.Matches(File.ReadAllText(view)))
                {
                    offenders.Add($"{Path.GetRelativePath(root, view)}: {tag.Value}");
                }
            }

            Assert.True(offenders.Count == 0, "Còn nạp CSS/JS từ máy chủ ngoài:\n" + string.Join("\n", offenders));
        }

        // CSS trong wwwroot/lib trỏ tới font bằng url() tương đối: file woff2 phải có thật.
        [Fact]
        public void LibStylesheets_Woff2Urls_PointAtExistingFiles()
        {
            var lib = Path.Combine(ViewSource.WebRoot(), "wwwroot", "lib");
            var missing = new List<string>();
            var found = 0;
            foreach (var css in Directory.EnumerateFiles(lib, "*.css", SearchOption.AllDirectories))
            {
                foreach (Match m in Regex.Matches(File.ReadAllText(css), @"url\((['""]?)(?!data:)([^)'""?#]+\.woff2)[^)]*\)"))
                {
                    found++;
                    var target = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(css)!, m.Groups[2].Value.Replace('/', Path.DirectorySeparatorChar)));
                    if (!File.Exists(target))
                    {
                        missing.Add($"{Path.GetRelativePath(lib, css)}: {m.Groups[2].Value}");
                    }
                }
            }

            Assert.True(found >= 16, $"Chỉ tìm thấy {found} url() woff2 trong wwwroot/lib");
            Assert.True(missing.Count == 0, string.Join("\n", missing));
        }

        [Theory]
        [InlineData("/QuanLyNhaTro/QuanLyHopDong")] // DataTables, SweetAlert2, SignalR, flatpickr, Select2
        [InlineData("/QuanLyNhaTro/Dashboard")]     // Chart.js
        public async Task ManagerPage_ReferencesOnlyLocalAssets_AndEveryOneIsServed(string page)
        {
            var w = await BranchScopeKit.SeedAsync(_factory);
            var (staff, _) = await BranchScopeKit.LoginAsync(_factory, w.Staff);

            var html = await (await staff.GetAsync(page)).Content.ReadAsStringAsync();

            Assert.DoesNotMatch(ExternalAsset, html);

            var local = Regex.Matches(html, "<(?:link|script)\\b[^>]*\\b(?:href|src)=\"(/lib/[^\"]+)\"")
                .Select(m => WebUtility.HtmlDecode(m.Groups[1].Value)).Distinct().ToList();
            Assert.True(local.Count >= 8, $"{page}: chỉ thấy {local.Count} file /lib");
            var client = _factory.CreateClient();
            foreach (var url in local)
            {
                Assert.Contains("?v=", url);
                var response = await client.GetAsync(url);
                Assert.True(response.StatusCode == HttpStatusCode.OK, $"{url}: {(int)response.StatusCode}");
                Assert.Equal(TimeSpan.FromDays(365), response.Headers.CacheControl?.MaxAge);
            }
        }
    }
}
