using System;
using System.IO;
using System.Linq;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Web.IntegrationTests
{
    // Rà soát sau năm đợt thiết kế lại giao diện (14/11/2026): các chỗ sót nhỏ đã sửa.
    // Chỉ đọc file nguồn, không cần DB hay server.
    public class RaSoatGiaoDienTests
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
        [InlineData("Views", "Shared", "_Sidebar.cshtml")]
        [InlineData("Areas", "KhachThue", "Views", "Shared", "_SidebarKhachThue.cshtml")]
        public void LogoThanhBen_DungMauThuongHieu(params string[] path)
        {
            var sidebar = Read(path);
            Assert.DoesNotContain("#2563eb", sidebar);
            Assert.DoesNotContain("#1d4ed8", sidebar);
            Assert.Contains("ts-logo-dau", sidebar);
            Assert.Contains("ts-logo-cuoi", sidebar);

            var nen = Read("wwwroot", "css", "trosync-nen.css");
            Assert.Contains(".ts-logo-dau { stop-color: var(--ts-brand); }", nen);
        }

        [Fact]
        public void TieuDeTab_KhopTieuDeTrang()
        {
            Assert.Contains("ViewData[\"Title\"] = \"Hóa đơn\";", Read("Areas", "QuanLyNhaTro", "Views", "HoaDon", "Index.cshtml"));
            Assert.Contains("ViewData[\"Title\"] = \"Bảng điều khiển\";", Read("Areas", "QuanLyNhaTro", "Views", "Dashboard", "Index.cshtml"));
        }

        [Fact]
        public void DienNuoc_KyMacDinhLayTuTimeProvider()
        {
            var view = Read("Areas", "QuanLyNhaTro", "Views", "DienNuoc", "Index.cshtml");
            Assert.DoesNotContain("DateTime.UtcNow", view);
            Assert.Contains("@inject TimeProvider TimeProvider", view);
        }

        [Fact]
        public void DienNuoc_CanhBaoChiSo_KhongKeoGianCot()
        {
            // Câu cảnh báo dài từng kéo cột "Nước mới" giãn ngang làm vỡ bảng.
            var css = Read("wwwroot", "css", "diennuoc.css");
            Assert.Contains(".diennuoc-input-td .dn-anomaly {", css);
            Assert.Contains("min-width: 100%;", css);

            var view = Read("Areas", "QuanLyNhaTro", "Views", "DienNuoc", "Index.cshtml");
            Assert.Contains("Đổi đồng hồ hoặc quay vòng: mở ảnh đồng hồ.", view);
        }

        [Fact]
        public void NhanNhomMenu_KhongAnVoiTrinhDocManHinh()
        {
            var menu = Read("Views", "Shared", "_MenuItem.cshtml");
            Assert.Contains("class=\"ts-nav-group\"", menu);
            Assert.DoesNotContain("ts-nav-group\" aria-hidden", menu);
        }

        [Theory]
        [InlineData("Areas", "QuanLyNhaTro", "Views", "PhongTros", "QuanLyPhongTro.cshtml")]
        [InlineData("wwwroot", "js", "doi-mat-khau.js")]
        public void IconCho_DungLopQuayCoThat(params string[] path)
        {
            // Tabler Icons không có ti-spin; Font Awesome (đã nạp trong hai layout) có fa-spin.
            Assert.DoesNotContain("ti-spin", Read(path));
        }
    }
}
