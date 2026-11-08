using System.Net;
using QuanLyChoThuePhongTroWeb.Web.IntegrationTests.Fixtures;
using Xunit;
using static QuanLyChoThuePhongTroWeb.Web.IntegrationTests.BranchScopeKit;

namespace QuanLyChoThuePhongTroWeb.Web.IntegrationTests
{
    // Thư viện chỉ nạp ở trang dùng (docs/features/toi-uu-hieu-nang): kiểm HTML thật có thư viện, đúng thứ tự
    // (thư viện -> cấu hình tiếng Việt của Select2 ở layout -> script của trang), và trang khác không còn nạp.
    [Collection(WebTestCollection.Name)]
    public class LayoutLibraryPageTests
    {
        private readonly CustomWebApplicationFactory _factory;

        public LayoutLibraryPageTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
        }

        private static async Task<string> HtmlAsync(HttpClient client, string url)
        {
            var response = await client.GetAsync(url);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return await response.Content.ReadAsStringAsync();
        }

        private static void InOrder(string html, params string[] parts)
        {
            var last = -1;
            foreach (var part in parts)
            {
                var index = html.IndexOf(part, StringComparison.Ordinal);
                Assert.True(index >= 0, $"Thiếu \"{part}\"");
                Assert.True(index > last, $"\"{part}\" phải đứng sau phần trước");
                last = index;
            }
        }

        [Fact]
        public async Task ContractPage_LoadsFlatpickrAndSelect2_BeforeLayoutConfigAndPageScript()
        {
            var w = await SeedAsync(_factory);
            var (admin, _) = await LoginAsync(_factory, w.Admin);

            var html = await HtmlAsync(admin, "/QuanLyNhaTro/QuanLyHopDong");

            InOrder(html, "flatpickr/4.6.13/flatpickr.min.css", "select2/4.0.13/css/select2.min.css");
            InOrder(html, "flatpickr/4.6.13/flatpickr.min.js", "flatpickr/4.6.13/l10n/vn.min.js", "select2/4.0.13/js/select2.full.min.js",
                "$.fn.select2.defaults.set('language'", "flatpickr(\"#filterDateRange\"");
            Assert.DoesNotContain("chart.js", html);
        }

        [Fact]
        public async Task Dashboard_LoadsPinnedChartJs_BeforeDashboardScript_AndNoDatePickerOrSelect2()
        {
            var w = await SeedAsync(_factory);
            var (admin, _) = await LoginAsync(_factory, w.Admin);

            var html = await HtmlAsync(admin, "/QuanLyNhaTro/Dashboard");

            InOrder(html, "npm/chart.js@4.6.1", "/js/dashboard.js");
            Assert.DoesNotContain("flatpickr", html);
            Assert.DoesNotContain("select2.full.min.js", html);
        }

        [Fact]
        public async Task InvoicePage_LoadsNoneOfThePageOnlyLibraries()
        {
            var w = await SeedAsync(_factory);
            var (admin, _) = await LoginAsync(_factory, w.Admin);

            var html = await HtmlAsync(admin, "/QuanLyNhaTro/QuanLyHoaDon");

            Assert.DoesNotContain("chart.js", html);
            Assert.DoesNotContain("flatpickr.min.js", html);
            Assert.DoesNotContain("select2.full.min.js", html);
        }
    }
}
