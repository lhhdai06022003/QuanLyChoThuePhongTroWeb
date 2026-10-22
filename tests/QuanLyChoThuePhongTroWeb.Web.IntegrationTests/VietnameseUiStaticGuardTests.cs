using System.Text.RegularExpressions;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Web.IntegrationTests
{
    // Giữ giao diện không quay lại chữ tiếng Anh mặc định của thư viện. Chỉ đọc file nguồn, không cần DB hay server.
    public class VietnameseUiStaticGuardTests
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

        private static string Read(params string[] relative) =>
            File.ReadAllText(Path.Combine(new[] { WebRoot() }.Concat(relative).ToArray()));

        [Fact]
        public void Views_HaveNoEnglishAriaLabels()
        {
            var english = new Regex("aria-label=\"(Close|Toggle navigation|Open user menu)\"");
            var offenders = Directory.EnumerateFiles(Path.Combine(WebRoot(), "Views"), "*.cshtml", SearchOption.AllDirectories)
                .Concat(Directory.EnumerateFiles(Path.Combine(WebRoot(), "Areas"), "*.cshtml", SearchOption.AllDirectories))
                .Where(f => english.IsMatch(File.ReadAllText(f)))
                .Select(f => Path.GetRelativePath(WebRoot(), f))
                .ToList();

            Assert.True(offenders.Count == 0, "aria-label còn tiếng Anh: " + string.Join(", ", offenders));
        }

        [Theory]
        [InlineData("Views", "Shared", "_Layout.cshtml")]
        [InlineData("Areas", "KhachThue", "Views", "Shared", "_LayoutKhachThue.cshtml")]
        public void Layouts_WrapSweetAlert_WithVietnameseDefaultButtons_AndDataTablesErrorText(params string[] path)
        {
            var layout = Read(path);

            Assert.Contains("confirmButtonText: 'Đóng'", layout);
            Assert.Contains("cancelButtonText: 'Hủy bỏ'", layout);
            Assert.Contains("typeof args[0] === 'string'", layout);
            Assert.Contains("$.fn.dataTable.ext.errMode", layout);
            Assert.Contains("Không tải được dữ liệu bảng", layout);
        }

        [Fact]
        public void ManagerLayout_SetsVietnameseSelect2Messages()
        {
            var layout = Read("Views", "Shared", "_Layout.cshtml");

            Assert.Contains("$.fn.select2.defaults.set('language'", layout);
            Assert.Contains("Không tìm thấy kết quả", layout);
        }

        [Fact]
        public void ErrorPage_IsVietnamese()
        {
            var page = Read("Views", "Shared", "Error.cshtml");

            Assert.Contains("Đã xảy ra lỗi khi xử lý yêu cầu của bạn", page);
            Assert.DoesNotContain("An error occurred", page);
            Assert.DoesNotContain("Request ID", page);
        }

        [Fact]
        public void Menu_DoesNotUseTheEnglishDashboardLabel()
        {
            var menu = Read("Services", "MenuService.cs");

            Assert.DoesNotContain("Name = \"Dashboard\"", menu);
            Assert.Contains("Name = \"Bảng điều khiển\"", menu);
        }
    }
}
