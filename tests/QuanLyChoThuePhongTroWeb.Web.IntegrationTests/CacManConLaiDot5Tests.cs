using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Web.IntegrationTests
{
    // Đợt 5 thiết kế lại giao diện: các màn còn lại của cổng quản lý và các màn con cổng khách thuê.
    // Chỉ đọc file nguồn, không cần DB hay server.
    public class CacManConLaiDot5Tests
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

        // 1. Theory trên các view đã làm đầy đủ ở V2–V4, V6, V7: không còn bg-light, btn-success, table-bordered, thead-light, modal-header bg-, confirmButtonColor, cancelButtonColor, font-weight-, text-right, page-title text-primary.
        [Theory]
        [InlineData("Areas/QuanLyNhaTro/Views/ChiNhanhs/QuanLyChiNhanh.cshtml")]
        [InlineData("Areas/QuanLyNhaTro/Views/NguoiDung/QuanLyTaiKhoanDangNhap.cshtml")]
        [InlineData("Areas/QuanLyNhaTro/Views/PhanCongChiNhanh/Index.cshtml")]
        [InlineData("Areas/QuanLyNhaTro/Views/DichVu/Index.cshtml")]
        [InlineData("Areas/QuanLyNhaTro/Views/DieuKhoanMau/Index.cshtml")]
        [InlineData("Areas/QuanLyNhaTro/Views/YeuCauSuCo/Index.cshtml")]
        [InlineData("Areas/QuanLyNhaTro/Views/ThongBao/Index.cshtml")]
        [InlineData("Areas/QuanLyNhaTro/Views/ViecCanLam/Index.cshtml")]
        [InlineData("Areas/QuanLyNhaTro/Views/DoiMatKhau/Index.cshtml")]
        [InlineData("Areas/KhachThue/Views/HoaDon/Index.cshtml")]
        [InlineData("Areas/KhachThue/Views/LichSuThanhToan/Index.cshtml")]
        [InlineData("Areas/KhachThue/Views/ChiSoDienNuoc/Index.cshtml")]
        [InlineData("Areas/KhachThue/Views/ThanhToanHoaDon/VnPayKetQua.cshtml")]
        [InlineData("Areas/KhachThue/Views/HopDong/Index.cshtml")]
        [InlineData("Areas/KhachThue/Views/HoSo/Index.cshtml")]
        [InlineData("Areas/KhachThue/Views/SuCo/Index.cshtml")]
        [InlineData("Areas/KhachThue/Views/ThongBao/Index.cshtml")]
        public void CacViewDot5_KhongConLopVaThuocTinhCu(string relativePath)
        {
            var content = Read(relativePath.Split('/'));
            Assert.DoesNotMatch(@"\bbg-light\b", content);
            Assert.DoesNotMatch(@"\bbtn-success\b", content);
            Assert.DoesNotMatch(@"\btable-bordered\b", content);
            Assert.DoesNotMatch(@"\bthead-light\b", content);
            Assert.DoesNotMatch(@"modal-header\s+bg-", content);
            Assert.DoesNotContain("confirmButtonColor", content);
            Assert.DoesNotContain("cancelButtonColor", content);
            Assert.DoesNotMatch(@"\bfont-weight-", content);
            Assert.DoesNotMatch(@"\btext-right\b", content);
            Assert.DoesNotMatch(@"page-title[^""]*\btext-primary\b", content);
        }

        // 2. Theory bảng chính có ts-bang-the:
        // chiNhanhTable, nguoiDungTable, phanCongTable, dichVuTable, bangGiaTable, dieuKhoanTable,
        // suCoTable, thongBaoTable (hai cổng), hoaDonTable và lichSuTable của khách thuê, suCoKhachThueTable, hopDongTable của khách thuê.
        [Theory]
        [InlineData("Areas/QuanLyNhaTro/Views/ChiNhanhs/QuanLyChiNhanh.cshtml", "chiNhanhTable")]
        [InlineData("Areas/QuanLyNhaTro/Views/NguoiDung/QuanLyTaiKhoanDangNhap.cshtml", "nguoiDungTable")]
        [InlineData("Areas/QuanLyNhaTro/Views/PhanCongChiNhanh/Index.cshtml", "phanCongTable")]
        [InlineData("Areas/QuanLyNhaTro/Views/DichVu/Index.cshtml", "dichVuTable")]
        [InlineData("Areas/QuanLyNhaTro/Views/DichVu/Index.cshtml", "bangGiaTable")]
        [InlineData("Areas/QuanLyNhaTro/Views/DieuKhoanMau/Index.cshtml", "dieuKhoanTable")]
        [InlineData("Areas/QuanLyNhaTro/Views/YeuCauSuCo/Index.cshtml", "suCoTable")]
        [InlineData("Areas/QuanLyNhaTro/Views/ThongBao/Index.cshtml", "thongBaoTable")]
        [InlineData("Areas/KhachThue/Views/ThongBao/Index.cshtml", "thongBaoTable")]
        [InlineData("Areas/KhachThue/Views/HoaDon/Index.cshtml", "hoaDonTable")]
        [InlineData("Areas/KhachThue/Views/LichSuThanhToan/Index.cshtml", "lichSuTable")]
        [InlineData("Areas/KhachThue/Views/SuCo/Index.cshtml", "suCoKhachThueTable")]
        [InlineData("Areas/KhachThue/Views/HopDong/Index.cshtml", "hopDongTable")]
        public void BangChinh_CoLopBangThe(string relativePath, string tableId)
        {
            var content = Read(relativePath.Split('/'));
            var tableRegex = new Regex(@"<table\b[^>]*>");
            var matches = tableRegex.Matches(content);
            var targetTable = matches.Cast<Match>().FirstOrDefault(m =>
                Regex.IsMatch(m.Value, $@"id=[""']{tableId}[""']"));

            Assert.True(targetTable != null, $"Không tìm thấy table có id='{tableId}' trong {relativePath}");
            Assert.Matches(@"\bts-bang-the\b", targetTable.Value);
        }

        // 3. Nút thêm không còn d-none: btnOpenAddModal ở Chi nhánh, Tài khoản, Điều khoản; nút mở #createSuCoModal của khách thuê.
        [Theory]
        [InlineData("Areas/QuanLyNhaTro/Views/ChiNhanhs/QuanLyChiNhanh.cshtml", "btnOpenAddModal")]
        [InlineData("Areas/QuanLyNhaTro/Views/NguoiDung/QuanLyTaiKhoanDangNhap.cshtml", "btnOpenAddModal")]
        [InlineData("Areas/QuanLyNhaTro/Views/DieuKhoanMau/Index.cshtml", "btnOpenAddModal")]
        public void NutThem_KhongConDNone(string relativePath, string buttonId)
        {
            var content = Read(relativePath.Split('/'));
            var pattern = $@"<button[^>]*id=[""']{buttonId}[""'][^>]*>";
            var match = Regex.Match(content, pattern);
            Assert.True(match.Success, $"Không tìm thấy nút {buttonId} trong {relativePath}");
            Assert.DoesNotMatch(@"\bd-none\b", match.Value);
        }

        [Fact]
        public void SuCoKhachThue_NutBaoCaoMoi_KhongConDNone()
        {
            var content = Read("Areas", "KhachThue", "Views", "SuCo", "Index.cshtml");
            var match = Regex.Match(content, @"<button[^>]*data-bs-target=[""']#createSuCoModal[""'][^>]*>");
            Assert.True(match.Success, "Không tìm thấy nút mở #createSuCoModal");
            Assert.DoesNotMatch(@"\bd-none\b", match.Value);
        }

        // 4. Hai view Thông báo không còn cdn.datatables.net/1.13.6/js.
        [Theory]
        [InlineData("Areas/QuanLyNhaTro/Views/ThongBao/Index.cshtml")]
        [InlineData("Areas/KhachThue/Views/ThongBao/Index.cshtml")]
        public void ThongBao_KhongConCdnDataTables(string relativePath)
        {
            var content = Read(relativePath.Split('/'));
            Assert.DoesNotContain("cdn.datatables.net/1.13.6/js", content);
        }

        // 5. bao-cao.js không còn #206bc4, #f59f00; BaoCao/Index.cshtml không có ts-bang-the.
        [Fact]
        public void BaoCao_KhongConMauHexCung_VaBangBaoCaoGiuCuonNgang()
        {
            var js = Read("wwwroot", "js", "bao-cao.js");
            Assert.DoesNotContain("#206bc4", js, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("#f59f00", js, StringComparison.OrdinalIgnoreCase);

            var view = Read("Areas", "QuanLyNhaTro", "Views", "BaoCao", "Index.cshtml");
            Assert.DoesNotContain("ts-bang-the", view);
        }

        // 6. YeuCauSuCo/Index.cshtml không còn ToString("N0"); Areas/KhachThue/Views/SuCo/Index.cshtml không còn AddHours(7).
        [Fact]
        public void SuCo_DinhDangTienVaGioVietNamChuan()
        {
            var qlView = Read("Areas", "QuanLyNhaTro", "Views", "YeuCauSuCo", "Index.cshtml");
            Assert.DoesNotContain("ToString(\"N0\")", qlView);

            var ktView = Read("Areas", "KhachThue", "Views", "SuCo", "Index.cshtml");
            Assert.DoesNotContain("AddHours(7)", ktView);
        }

        // 7. Theory giữ id cho màn Hóa đơn khách thuê: liệt kê mọi id mà khachthue-thanhtoan.js và khachthue-vnpay.js dùng
        [Fact]
        public void HoaDonKhachThue_GiuDuCacIdNghiepVuVaThanhToan()
        {
            var view = Read("Areas", "KhachThue", "Views", "HoaDon", "Index.cshtml");
            foreach (var id in new[] { "btnDownloadPdf", "chiTietBody", "chiTietFooter", "chiTietModal", "hoaDonTable", "ktPaymentPanel" })
            {
                Assert.Contains($"id=\"{id}\"", view);
            }

            var js1 = Read("wwwroot", "js", "khachthue-thanhtoan.js");
            var js2 = Read("wwwroot", "js", "khachthue-vnpay.js");
            var combinedJs = js1 + "\n" + js2;
            foreach (var id in new[] { "ktPayBankGrid", "ktPayRecheck", "ktPayShowBanks", "ktPayTabQr", "ktPayTabVnPay", "ktPayVnPay", "ktVnPayRecheck" })
            {
                Assert.Contains($"#{id}", combinedJs);
            }
        }

        // 8. Các view khách thuê có CSS riêng (HoaDon, ChiSoDienNuoc, VnPayKetQua): <link nằm trong @section Styles.
        [Theory]
        [InlineData("Areas/KhachThue/Views/HoaDon/Index.cshtml")]
        [InlineData("Areas/KhachThue/Views/ChiSoDienNuoc/Index.cshtml")]
        [InlineData("Areas/KhachThue/Views/ThanhToanHoaDon/VnPayKetQua.cshtml")]
        public void ViewKhachThue_LinkCssNamTrongSectionStyles(string relativePath)
        {
            var content = Read(relativePath.Split('/'));
            var sectionIdx = content.IndexOf("@section Styles", StringComparison.Ordinal);
            Assert.True(sectionIdx >= 0, $"{relativePath} thiếu @section Styles");
            foreach (Match m in Regex.Matches(content, @"<link[^>]+rel=[""']stylesheet[""']"))
            {
                Assert.True(m.Index > sectionIdx, $"<link> ở vị trí {m.Index} trong {relativePath} nằm ngoài @section Styles");
            }
        }

        // 9. S1: Tab Bảng giá theo chi nhánh của màn Dịch vụ vẽ lại khi chuyển tab để gán đầy đủ data-label cho thẻ trên điện thoại
        [Fact]
        public void DichVu_TabBangGia_VeLaiKhiChuyenTabDeGanNhanThe()
        {
            var content = Read("Areas", "QuanLyNhaTro", "Views", "DichVu", "Index.cshtml");
            Assert.Contains("shown.bs.tab", content);
            Assert.Contains("tableBangGia.columns.adjust().draw", content);
            Assert.Matches(@"#tab-bang-gia['""].*shown\.bs\.tab", content);
        }
    }
}
