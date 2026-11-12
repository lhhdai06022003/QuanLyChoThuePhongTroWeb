using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Web.IntegrationTests
{
    // Đợt 3 thiết kế lại giao diện: nhóm thu tiền dùng màu nền chung, bảng thành thẻ trên điện thoại,
    // Lịch sử thanh toán không còn ô màu đặc. Chỉ đọc file nguồn, không cần DB hay server.
    public class NhomThuTienDot3Tests
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

        private static string View(string folder) => Read("Areas", "QuanLyNhaTro", "Views", folder, "Index.cshtml");

        [Theory]
        [InlineData("hoadon.css")]
        [InlineData("diennuoc.css")]
        public void CssNhomThuTien_KhongConMauTealRieng(string file)
        {
            var css = Read("wwwroot", "css", file);
            Assert.DoesNotContain("#0f6b63", css, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("#3fb8ac", css, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("--tblr-primary:", css);
        }

        [Fact]
        public void HoaDonCss_KhongEpMauNutXacNhanHopThoai()
        {
            // Nút xác nhận theo quy ước của layout: xanh thương hiệu, đỏ cho hộp cảnh báo (người dùng chọn 12/11/2026).
            Assert.DoesNotContain("swal2-confirm", Read("wwwroot", "css", "hoadon.css"));
        }

        [Theory]
        [InlineData("HoaDon")]
        [InlineData("DienNuoc")]
        [InlineData("LichSuThanhToan")]
        [InlineData("DoiChieuThanhToan")]
        [InlineData("GiaoDichSePay")]
        public void BangNhomThuTien_ChuyenTheTrenDienThoai(string folder)
        {
            Assert.Matches("<table[^>]*class=\"[^\"]*\\bts-bang-the\\b", View(folder));
        }

        [Fact]
        public void LopBangThe_KhaiBaoDuoi768VaSiteJsGanNhan()
        {
            var css = Read("wwwroot", "css", "trosync-nen.css");
            var media = css.IndexOf("@media (max-width: 767.98px)", css.IndexOf(".ts-o-so-gia-tri", StringComparison.Ordinal), StringComparison.Ordinal);
            Assert.True(media > 0, "Thiếu media query của .ts-bang-the");
            Assert.Contains("table.ts-bang-the tbody td:empty", css[media..]);
            Assert.Contains(".table-responsive:has(.ts-bang-the)", css[media..]);

            var js = Read("wwwroot", "js", "site.js");
            Assert.Contains("window.TroBangThe", js);
            Assert.Contains("draw.dt", js);
            Assert.Contains("data-the-nhan", js);
        }

        [Fact]
        public void LichSuThanhToan_KhongConOMauDac_GiuDuId()
        {
            var html = View("LichSuThanhToan");
            Assert.DoesNotMatch("class=\"[^\"]*\\bbg-(success|primary|info|indigo|azure|orange|teal)\\b[^\"]*\\btext-white", html);
            Assert.DoesNotContain("style=\"overflow-x", html);
            foreach (var id in new[] { "statTongTitle", "statTongDoanhThu", "statTongChuThich", "statTongSoGiaoDich", "statDoanhThuTienMat", "statDoanhThuChuyenKhoan", "statDoanhThuSePay", "statDoanhThuVnPay", "statDoanhThuCanTruCoc" })
                Assert.Contains($"id=\"{id}\"", html);
        }

        [Theory]
        [InlineData("btnAddTruyThuDien")]
        [InlineData("btnAddTruyThuNuoc")]
        public void HoaDon_NutTruyThuVanAnBangStyleDeJsToggle(string id)
        {
            // JS bật nút bằng .toggle(); đổi sang d-none thì nút không bao giờ hiện lại.
            Assert.Matches($"id=\"{id}\"[^>]*style=\"display: none;\"", View("HoaDon"));
        }

        [Fact]
        public void DoiChieu_KhongThemMuiGioVaKhongEpMauNut()
        {
            // Server trả giờ Việt Nam không kèm offset (DateTimeJsonConverter): thêm timeZone sẽ lệch giờ.
            var js = Read("wwwroot", "js", "doi-chieu-thanh-toan.js");
            Assert.DoesNotContain("timeZone", js);
            Assert.DoesNotContain("confirmButtonColor", js);
            Assert.DoesNotContain("bg-orange text-white", js);
        }

        [Theory]
        [InlineData("DienNuoc")]
        [InlineData("DoiChieuThanhToan")]
        [InlineData("GiaoDichSePay")]
        public void LinkCss_NamTrongSectionStyles(string folder)
        {
            var html = View(folder);
            var section = html.IndexOf("@section Styles", StringComparison.Ordinal);
            Assert.True(section >= 0, "Thiếu @section Styles");
            foreach (Match m in Regex.Matches(html, "<link[^>]+rel=\"stylesheet\""))
                Assert.True(m.Index > section, $"<link> ở vị trí {m.Index} nằm ngoài @section Styles");
        }
    }
}
