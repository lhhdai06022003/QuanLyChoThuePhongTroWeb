using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Web.IntegrationTests.Fixtures;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Web.IntegrationTests
{
    // Bản dịch tiếng Việt của DataTables nằm ở wwwroot/js/datatables-vi.json (docs/features/toi-uu-hieu-nang, bước 14):
    // bảng không còn chờ cdn.datatables.net trước khi khởi tạo. Layout đặt window.dataTablesViUrl kèm ?v= để cache lâu.
    [Collection(WebTestCollection.Name)]
    public class DataTablesLanguageTests
    {
        private readonly CustomWebApplicationFactory _factory;

        public DataTablesLanguageTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
        }

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
        public void NoViewOrScript_LoadsLanguageFromCdn()
        {
            var root = WebRoot();
            var files = Directory.EnumerateFiles(Path.Combine(root, "Areas"), "*.cshtml", SearchOption.AllDirectories)
                .Concat(Directory.EnumerateFiles(Path.Combine(root, "Views"), "*.cshtml", SearchOption.AllDirectories))
                .Concat(Directory.EnumerateFiles(Path.Combine(root, "wwwroot", "js"), "*.js", SearchOption.AllDirectories));

            var offenders = files
                .Where(f => File.ReadAllText(f).Contains("cdn.datatables.net/plug-ins"))
                .Select(f => Path.GetRelativePath(root, f))
                .ToList();

            Assert.True(offenders.Count == 0, "Còn tải bản dịch DataTables từ CDN:\n" + string.Join("\n", offenders));
        }

        [Fact]
        public void LocalLanguageFile_HasTheKeysTablesUse()
        {
            using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(WebRoot(), "wwwroot", "js", "datatables-vi.json")));
            var root = json.RootElement;

            foreach (var key in new[] { "processing", "emptyTable", "info", "infoEmpty", "infoFiltered", "lengthMenu", "loadingRecords", "search", "zeroRecords" })
            {
                Assert.False(string.IsNullOrWhiteSpace(root.GetProperty(key).GetString()), key);
            }
            Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("paginate").GetProperty("next").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("paginate").GetProperty("previous").GetString()));
        }

        [Fact]
        public async Task ManagerAndTenantLayouts_PointTablesAtVersionedLocalFile()
        {
            var w = await BranchScopeKit.SeedAsync(_factory);
            var (staff, _) = await BranchScopeKit.LoginAsync(_factory, w.Staff);

            var html = await (await staff.GetAsync("/QuanLyNhaTro/QuanLyHoaDon")).Content.ReadAsStringAsync();
            var match = Regex.Match(html, @"window\.dataTablesViUrl = '(/js/datatables-vi\.json\?v=[A-Za-z0-9_-]+)';");
            Assert.True(match.Success, "Layout quản lý không đặt window.dataTablesViUrl kèm ?v=");

            var response = await _factory.CreateClient().GetAsync(match.Groups[1].Value);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
            Assert.Equal(TimeSpan.FromDays(365), response.Headers.CacheControl?.MaxAge);
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.True(body.RootElement.TryGetProperty("paginate", out _));

            var tenantLayout = File.ReadAllText(Path.Combine(WebRoot(), "Areas", "KhachThue", "Views", "Shared", "_LayoutKhachThue.cshtml"));
            Assert.Contains("window.dataTablesViUrl", tenantLayout);
        }
    }
}
