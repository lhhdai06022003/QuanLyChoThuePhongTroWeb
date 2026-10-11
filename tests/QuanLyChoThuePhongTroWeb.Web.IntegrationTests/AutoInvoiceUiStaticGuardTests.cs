using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using QuanLyChoThuePhongTroWeb.Web.IntegrationTests.Fixtures;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Web.IntegrationTests
{
    // Kiểm tra tĩnh phần JS/HTML phát hành cho giao diện hóa đơn tự động (đợt C).
    // Không chạy JS: chỉ chốt các điều kiện quan trọng có trong tài nguyên mà server trả về.
    [Collection(WebTestCollection.Name)]
    public class AutoInvoiceUiStaticGuardTests
    {
        private readonly CustomWebApplicationFactory _factory;

        public AutoInvoiceUiStaticGuardTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
        }

        private HttpClient Client() => _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        [Fact]
        public async Task AdjustScript_IsServed_SendsAntiforgeryHeader_EscapesHtml_AndNeverUsesAlert()
        {
            var res = await Client().GetAsync("/js/hoadon-dieuchinh.js");
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            var js = await res.Content.ReadAsStringAsync();

            Assert.Contains("'RequestVerificationToken': token()", js);
            Assert.Contains("/HoaDon/DieuChinh/", js);
            Assert.Contains("esc(s.lyDoKhoa)", js);
            Assert.Contains("esc(res.message)", js);
            Assert.Contains("esc(d.maHoaDon)", js);
            Assert.Contains("esc(d.hanTuChot)", js);
            Assert.Contains(".fail(", js);
            Assert.DoesNotContain("alert(", Regex.Replace(js, @"Swal\.fire|\.alert-|alert-", string.Empty));
            // Không có ô lý do trong modal điều chỉnh.
            Assert.DoesNotContain("lyDo\"", js);
            Assert.Contains("Không có thay đổi.", js);
        }

        [Fact]
        public async Task AdjustScript_EditsFeeLines_PreviewsMeterLines_AndStaysWithinServerLimits()
        {
            var js = await (await Client().GetAsync("/js/hoadon-dieuchinh.js")).Content.ReadAsStringAsync();

            // Gửi danh sách dòng khoản phí và hỏi server tính lại dòng điện nước theo chỉ số mới.
            Assert.Contains("chiTiets", js);
            Assert.Contains("/TinhLai", js);
            // Cùng giới hạn với server: tên dòng tối đa 500 ký tự, số lượng 3 chữ số thập phân, đơn giá nguyên đồng.
            Assert.Contains("maxlength=\"500\"", js);
            Assert.Contains("step=\"0.001\"", js);
            Assert.Contains("Đơn giá phải là số nguyên VND.", js);
            // Tên khoản phí và các ô nhập do người dùng gõ chỉ đưa vào DOM qua .val()/.text(), không ghép HTML.
            Assert.DoesNotContain("'<input type=\"text\" value=\"", js);
        }

        [Theory]
        [InlineData("/js/hoadon-dieuchinh.js")]
        public async Task AdjustScript_DoesNotInjectServerFieldsWithoutEscape(string path)
        {
            var js = await (await Client().GetAsync(path)).Content.ReadAsStringAsync();
            // Các chỗ dùng .html(...) hoặc html: chỉ được ghép chuỗi đã qua esc().
            Assert.DoesNotContain("+ s.lyDoKhoa", js);
            Assert.DoesNotContain("+ res.message", js);
            Assert.DoesNotContain("+ d.maHoaDon", js);
        }
    }
}
