using System.Net;
using System.Text.RegularExpressions;
using QuanLyChoThuePhongTroWeb.Web.IntegrationTests.Fixtures;
using Xunit;
using static QuanLyChoThuePhongTroWeb.Web.IntegrationTests.BranchScopeKit;

namespace QuanLyChoThuePhongTroWeb.Web.IntegrationTests
{
    // Trang Hợp đồng in danh sách người thuê một lần dạng JSON; JS đổ vào ô lọc và ô chọn trong modal
    // trước khi khởi tạo Select2 (docs/features/toi-uu-hieu-nang, bước 8).
    [Collection(WebTestCollection.Name)]
    public class ContractPagePayloadTests
    {
        private readonly CustomWebApplicationFactory _factory;

        public ContractPagePayloadTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task TenantList_IsPrintedOnce_AsJson_AndFilledIntoBothSelects_BeforeSelect2()
        {
            var w = await SeedAsync(_factory);
            var (admin, _) = await LoginAsync(_factory, w.Admin);

            var response = await admin.GetAsync("/QuanLyNhaTro/QuanLyHopDong");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var html = await response.Content.ReadAsStringAsync();

            foreach (var tenantId in new[] { w.TenantA, w.TenantB, w.TenantFree })
            {
                Assert.Single(Regex.Matches(html, $"\"value\":\"{tenantId}\""));
            }

            // Hai ô chọn người thuê chỉ có lựa chọn mặc định viết sẵn trong HTML (không in <option> người thuê).
            // Không kiểm "<option value=\"{tenantId}\">" trên cả trang: ô chọn chi nhánh cũng in <option value="{id}">,
            // trên DB test trống mã chi nhánh có thể trùng mã người thuê (CI GitHub 16/11/2026).
            Assert.Matches("<select id=\"filterNguoiThue\"[^>]*>\\s*<option value=\"0\">-- Tất cả --</option>\\s*</select>", html);
            Assert.Matches("<select class=\"form-control select2\" id=\"NguoiThueId\"[^>]*>\\s*<option value=\"\">-- Chọn khách thuê --</option>\\s*</select>", html);

            // Dữ liệu JSON in trong trang, trước thẻ nạp wwwroot/js/quan-ly-hop-dong.js (mã của màn, bước 15).
            var data = html.IndexOf("window.hopDongConfig.allNguoiThues = [", StringComparison.Ordinal);
            var scriptTag = html.IndexOf("src=\"/js/quan-ly-hop-dong.js?v=", StringComparison.Ordinal);
            Assert.True(data > 0, "Thiếu dữ liệu JSON người thuê trong trang");
            Assert.True(scriptTag > data, "Phải có dữ liệu JSON trước khi nạp file JS của màn");

            var js = ViewSource.Read("wwwroot", "js", "quan-ly-hop-dong.js");
            var fill = js.IndexOf("new Option(nt.text, nt.value)", StringComparison.Ordinal);
            Assert.True(fill > js.IndexOf("let allNguoiThues = window.hopDongConfig.allNguoiThues;", StringComparison.Ordinal), "Phải đổ sau khi có dữ liệu JSON");
            Assert.True(fill > 0 && fill < js.IndexOf("$('#NguoiThueId').select2({", StringComparison.Ordinal), "Phải đổ trước khi khởi tạo Select2");
            Assert.True(fill < js.IndexOf("d.nguoiThueId = $('#filterNguoiThue').val();", StringComparison.Ordinal), "Phải đổ trước khi tạo bảng");
        }
    }
}
