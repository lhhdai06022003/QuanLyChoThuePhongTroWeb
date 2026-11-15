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
            ViewSource.ReadWithOwnScript(relative); // view kèm file JS riêng của màn (ViewSource.OwnScripts)

        // Kèm file JS riêng của màn (ViewSource.OwnScripts): bảng ghép chuỗi và phần ẩn/hiện nằm trong JS.
        private static string View(string folder) => ViewSource.ReadWithOwnScript("Areas", "QuanLyNhaTro", "Views", folder, "Index.cshtml");

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

        // Số chỗ ẩn bằng style mà JS bật lại bằng .toggle()/.show(); đổi sang d-none thì phần đó không bao giờ hiện.
        [Theory]
        [InlineData("HoaDon", "Index.cshtml", 2)]
        [InlineData("DienNuoc", "Index.cshtml", 7)]
        [InlineData("DienNuoc", "_AnhChiSoModal.cshtml", 15)]
        [InlineData("DoiChieuThanhToan", "Index.cshtml", 1)]
        public void AnBangStyle_GiuNguyenDeJsBatLai(string folder, string file, int soCho)
        {
            var html = ViewSource.ReadWithOwnScript("Areas", "QuanLyNhaTro", "Views", folder, file);
            Assert.Equal(soCho, Regex.Matches(html, "style=\"display: none;\"").Count);
        }

        [Fact]
        public void BangTuGhepChuoi_GhiSanNhanO()
        {
            // Điện nước và SePay không dùng DataTables draw.dt cho nhãn: mất data-label thì thẻ không có nhãn.
            var dienNuoc = View("DienNuoc");
            foreach (var nhan in new[] { "Điện mới", "Nước mới", "Điện cũ", "Nước cũ", "Người đại diện" })
                Assert.Contains($"data-label=\"{nhan}\"", dienNuoc);

            var sepay = Read("wwwroot", "js", "giao-dich-sepay.js");
            foreach (var nhan in new[] { "Thời gian", "Số tiền", "Hóa đơn", "Trạng thái" })
                Assert.Contains($"data-label=\"{nhan}\"", sepay);
        }

        [Fact]
        public void HoaDonCss_OCot44pxChiApTuMayTinh()
        {
            // Ô đầu cố định 44px đè lên bố cục thẻ nếu áp cả dưới 768px.
            var css = Read("wwwroot", "css", "hoadon.css");
            var media = css.IndexOf("@media (min-width: 768px)", StringComparison.Ordinal);
            var o44 = css.IndexOf("max-width: 44px", StringComparison.Ordinal);
            Assert.True(media >= 0 && o44 > media, "Quy tắc ô đầu 44px phải nằm trong @media (min-width: 768px)");
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
