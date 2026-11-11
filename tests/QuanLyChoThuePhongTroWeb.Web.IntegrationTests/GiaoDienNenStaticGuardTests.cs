using Xunit;

namespace QuanLyChoThuePhongTroWeb.Web.IntegrationTests
{
    // Giữ lớp vỏ giao diện mới (màu, font, cổng khách thuê) không bị lệch. Chỉ đọc file nguồn, không cần DB hay server.
    public class GiaoDienNenStaticGuardTests
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

        [Theory]
        [InlineData("Views", "Shared", "_Layout.cshtml")]
        [InlineData("Areas", "KhachThue", "Views", "Shared", "_LayoutKhachThue.cshtml")]
        [InlineData("Areas", "QuanLyNhaTro", "Views", "NguoiDung", "DangNhap.cshtml")]
        public void HaiCongVaManDangNhap_NapNenVaFontMoi(params string[] path)
        {
            var html = Read(path);
            Assert.Contains("~/css/trosync-nen.css", html);
            Assert.Contains("family=Be+Vietnam+Pro", html);
            Assert.DoesNotContain("family=Inter", html);
        }

        [Fact]
        public void CongKhachVangLai_KhongNapNenMoi()
        {
            var html = Read("Views", "Shared", "_PublicRoomLayout.cshtml");
            Assert.DoesNotContain("trosync-nen.css", html);
            Assert.Contains("family=Inter", html);
        }

        [Fact]
        public void FileNen_KhaiBaoDuBienChoSangVaToi()
        {
            var css = Read("wwwroot", "css", "trosync-nen.css");
            Assert.Contains("[data-bs-theme=\"dark\"]", css);
            Assert.Contains("'Be Vietnam Pro'", css);
            foreach (var bien in new[] { "--ts-brand:", "--ts-brand-text:", "--ts-brand-soft:", "--ts-ok:", "--ts-wait:", "--ts-bad:", "--tblr-primary:", "--tblr-primary-rgb:", "--tblr-body-bg:" })
                Assert.True(css.Split(bien).Length - 1 >= 2, $"{bien} phải có ở cả khối sáng và khối tối");
        }
    }
}
