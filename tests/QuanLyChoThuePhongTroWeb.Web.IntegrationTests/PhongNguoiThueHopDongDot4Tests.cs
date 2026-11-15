using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Web.IntegrationTests
{
    // Đợt 4 thiết kế lại giao diện: Phòng trọ, Người thuê, Hợp đồng dùng màu nền chung,
    // bảng thành thẻ trên điện thoại, stepper hợp đồng chuẩn biến màu.
    // Chỉ đọc file nguồn, không cần DB hay server.
    public class PhongNguoiThueHopDongDot4Tests
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

        [Theory]
        [InlineData("quanlyphongtro.css")]
        [InlineData("hopdong.css")]
        [InlineData("dang-ky-dich-vu.css")]
        public void CssDot4_KhongConMauCuVaBienLoiThoi(string file)
        {
            var css = Read("wwwroot", "css", file);
            var badTokens = new[] { "#206bc4", "#2fb344", "#f59f00", "#d63939", "#0d6efd", "--tb-primary", "pulse-badge" };
            foreach (var token in badTokens)
            {
                Assert.DoesNotContain(token, css, StringComparison.OrdinalIgnoreCase);
            }
        }

        [Theory]
        [InlineData("Areas/QuanLyNhaTro/Views/PhongTros/QuanLyPhongTro.cshtml")]
        [InlineData("Areas/QuanLyNhaTro/Views/NguoiThue/QuanLyNguoiThue.cshtml")]
        [InlineData("Areas/QuanLyNhaTro/Views/HopDong/QuanLyHopDong.cshtml")]
        public void ViewsDot4_BangChinhCoLopBangThe_VaKhongConLopLoiThoi(string path)
        {
            var html = ViewSource.ReadWithOwnScript(path.Split('/'));
            Assert.Matches("<table[^>]*class=\"[^\"]*\\bts-bang-the\\b", html);

            var forbidden = new[] { "bg-light", "btn-success", "table-bordered", "thead-light", "modal-header bg-", "confirmButtonColor", "text-right", "font-weight-" };
            foreach (var item in forbidden)
            {
                Assert.DoesNotContain(item, html);
            }
        }

        [Fact]
        public void NguoiThue_NutThemKhongChuaDNone()
        {
            var html = Read("Areas", "QuanLyNhaTro", "Views", "NguoiThue", "QuanLyNguoiThue.cshtml");
            var match = Regex.Match(html, @"<button[^>]*id=""btnOpenAddModal""[^>]*>");
            Assert.True(match.Success, "Không tìm thấy nút id=\"btnOpenAddModal\"");
            Assert.DoesNotContain("d-none", match.Value);
        }

        [Theory]
        [InlineData("Areas/QuanLyNhaTro/Views/PhongTros/QuanLyPhongTro.cshtml", new[] { "statTrong", "statDaThue", "statNoTien", "statSapHetHan", "btnGridView", "btnTableView", "roomGrid", "phongTroTable", "qpInvoiceId", "btnConfirmQuickPayment" })]
        [InlineData("Areas/QuanLyNhaTro/Views/HopDong/QuanLyHopDong.cshtml", new[] { "hopDongTable", "hopDongStepper", "btnSaveData", "filterChiNhanh", "filterNguoiThue", "filterDateRange", "filterTrangThai", "filterBoLoc" })]
        [InlineData("Areas/QuanLyNhaTro/Views/HopDongChiTiet/ThanhLy.cshtml", new[] { "ngayTraPhong", "btnLuuNgayTraPhong", "chkXacNhanConNo", "btnXacNhanThanhLy", "khuXacNhan", "khuKetQua", "conNoSauCanTru", "formBienBan" })]
        [InlineData("Areas/QuanLyNhaTro/Views/HopDongChiTiet/GiaHan.cshtml", new[] { "btnXacNhanGiaHan", "ngayKetThucMoi", "tienThue", "tienCoc", "nhacChenhLechCoc", "khuDieuKhoanMau" })]
        public void ViewsDot4_GiuNguyenCacIdNghiepVu(string path, string[] ids)
        {
            var html = Read(path.Split('/'));
            foreach (var id in ids)
            {
                Assert.Contains($"id=\"{id}\"", html);
            }
        }

        [Theory]
        [InlineData("Areas/QuanLyNhaTro/Views/HopDongChiTiet/ThanhLy.cshtml", 1)]
        [InlineData("Areas/QuanLyNhaTro/Views/HopDongChiTiet/GiaHan.cshtml", 2)]
        [InlineData("Areas/QuanLyNhaTro/Views/HopDong/QuanLyHopDong.cshtml", 3)]
        [InlineData("Areas/QuanLyNhaTro/Views/PhongTros/QuanLyPhongTro.cshtml", 2)]
        public void ViewsDot4_SoLuongDisplayNoneKhopMoc(string path, int expectedCount)
        {
            var html = ViewSource.ReadWithOwnScript(path.Split('/'));
            var count = Regex.Matches(html, @"display:\s*none", RegexOptions.IgnoreCase).Count;
            Assert.Equal(expectedCount, count);
        }

        [Fact]
        public void SiteJsVaTrosyncNen_KhaiBaoHamVaLopChung()
        {
            var js = Read("wwwroot", "js", "site.js");
            Assert.Contains("ganTatCa", js);
            Assert.Contains("DOMContentLoaded", js);

            var css = Read("wwwroot", "css", "trosync-nen.css");
            Assert.Contains("ts-cuon-ngang", css);
        }

        [Fact]
        public void DangKyDichVu_DungCssRieng_NamTrongSectionStyles()
        {
            var partial = Read("Areas", "QuanLyNhaTro", "Views", "Shared", "_DangKyDichVuPartial.cshtml");
            Assert.DoesNotContain("<style", partial, StringComparison.OrdinalIgnoreCase);

            foreach (var viewPath in new[] { "Areas/QuanLyNhaTro/Views/PhongTros/QuanLyPhongTro.cshtml", "Areas/QuanLyNhaTro/Views/HopDong/QuanLyHopDong.cshtml" })
            {
                var html = Read(viewPath.Split('/'));
                var sectionIdx = html.IndexOf("@section Styles", StringComparison.Ordinal);
                Assert.True(sectionIdx >= 0, $"Thiếu @section Styles trong {viewPath}");

                var linkIdx = html.IndexOf("dang-ky-dich-vu.css", StringComparison.Ordinal);
                Assert.True(linkIdx > sectionIdx, $"Link dang-ky-dich-vu.css phải nằm sau @section Styles trong {viewPath}");
            }
        }

        [Theory]
        [InlineData("Areas/QuanLyNhaTro/Views/PhongTros/QuanLyPhongTro.cshtml")]
        [InlineData("Areas/QuanLyNhaTro/Views/PhongTros/SoDoPhong.cshtml")]
        [InlineData("Areas/QuanLyNhaTro/Views/NguoiThue/QuanLyNguoiThue.cshtml")]
        [InlineData("Areas/QuanLyNhaTro/Views/HopDong/QuanLyHopDong.cshtml")]
        public void ViewsDot4_KhongConToLocaleDateString(string path)
        {
            var html = ViewSource.ReadWithOwnScript(path.Split('/'));
            Assert.DoesNotContain("toLocaleDateString(", html);
        }
    }
}
