using Xunit;

namespace QuanLyChoThuePhongTroWeb.Web.IntegrationTests
{
    // JS của các màn lớn đã tách khỏi view sang wwwroot/js (docs/features/toi-uu-hieu-nang, bước 15).
    // Test soát tĩnh từng đọc riêng file .cshtml phải đọc kèm file JS của màn, nếu không sẽ bỏ sót phần mã đã chuyển đi.
    internal static class ViewSource
    {
        // Đường dẫn view (tính từ thư mục project Web, dùng '/') → file JS riêng của màn trong wwwroot/js.
        public static readonly IReadOnlyDictionary<string, string> OwnScripts = new Dictionary<string, string>
        {
            ["Areas/QuanLyNhaTro/Views/DienNuoc/Index.cshtml"] = "chot-dien-nuoc.js",
            ["Areas/QuanLyNhaTro/Views/DienNuoc/_AnhChiSoModal.cshtml"] = "anh-chi-so-modal.js",
            ["Areas/QuanLyNhaTro/Views/HoaDon/Index.cshtml"] = "hoadon-danh-sach.js",
            ["Areas/QuanLyNhaTro/Views/PhongTros/QuanLyPhongTro.cshtml"] = "quan-ly-phong-tro.js",
            ["Areas/QuanLyNhaTro/Views/HopDong/QuanLyHopDong.cshtml"] = "quan-ly-hop-dong.js",
            ["Areas/QuanLyNhaTro/Views/Shared/_DangKyDichVuPartial.cshtml"] = "dang-ky-dich-vu.js",
            ["Areas/QuanLyNhaTro/Views/NguoiDung/QuanLyTaiKhoanDangNhap.cshtml"] = "quan-ly-tai-khoan.js",
            ["Areas/QuanLyNhaTro/Views/DichVu/Index.cshtml"] = "dich-vu.js",
            ["Areas/QuanLyNhaTro/Views/PhanCongChiNhanh/Index.cshtml"] = "phan-cong-chi-nhanh.js",
            ["Areas/QuanLyNhaTro/Views/NguoiThue/QuanLyNguoiThue.cshtml"] = "quan-ly-nguoi-thue.js",
            ["Areas/QuanLyNhaTro/Views/ChiNhanhs/QuanLyChiNhanh.cshtml"] = "quan-ly-chi-nhanh.js",
            ["Areas/QuanLyNhaTro/Views/ThongBao/Index.cshtml"] = "thong-bao-danh-sach.js",
            ["Areas/QuanLyNhaTro/Views/PhongTros/SoDoPhong.cshtml"] = "so-do-phong.js",
            ["Areas/QuanLyNhaTro/Views/DieuKhoanMau/Index.cshtml"] = "dieu-khoan-mau.js",
            ["Areas/KhachThue/Views/ChiSoDienNuoc/Index.cshtml"] = "khachthue-chi-so-dien-nuoc.js",
            ["Areas/KhachThue/Views/HoaDon/Index.cshtml"] = "khachthue-hoa-don.js",
            ["Areas/KhachThue/Views/ThongBao/Index.cshtml"] = "thong-bao-danh-sach.js",
            ["Areas/KhachThue/Views/HopDong/Index.cshtml"] = "khachthue-hop-dong.js"
        };

        public static string WebRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "QuanLyChoThuePhongTroWeb.sln")))
            {
                dir = dir.Parent;
            }
            Assert.NotNull(dir);
            return Path.Combine(dir!.FullName, "src", "QuanLyChoThuePhongTroWeb.Web");
        }

        public static string Read(params string[] relative) =>
            File.ReadAllText(Path.Combine(new[] { WebRoot() }.Concat(relative).ToArray()));

        // Nội dung view nối với file JS riêng của màn (nếu màn đã tách JS).
        public static string ReadWithOwnScript(params string[] relative)
        {
            var content = Read(relative);
            var key = string.Join("/", relative.SelectMany(p => p.Split('/', '\\')));
            return OwnScripts.TryGetValue(key, out var js)
                ? content + "\n" + Read("wwwroot", "js", js)
                : content;
        }

        // HTML của trang đã render nối với các file JS riêng của màn mà trang nạp (<script src="/js/<file>?v=...">),
        // cho test kiểm chuỗi JS trên trang trả về.
        public static string WithOwnScripts(string html)
        {
            var result = html;
            foreach (var js in OwnScripts.Values.Distinct())
            {
                if (html.Contains($"src=\"/js/{js}"))
                {
                    result += "\n" + Read("wwwroot", "js", js);
                }
            }
            return result;
        }
    }
}
