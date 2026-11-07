using System;
using System.Linq;
using System.Text;

namespace QuanLyChoThuePhongTroWeb.Application.Common.Security
{
    // Chính sách mật khẩu dùng chung (spec bảo mật xác thực §2.4) và nhận diện mật khẩu mặc định cần ép đổi (§2.2).
    public static class PasswordPolicy
    {
        public const int DoDaiToiThieu = 8;
        public const int DoDaiToiDa = 128;

        private const string MatKhauMacDinhAdmin = "admin";

        // Danh sách yếu chốt trong plan; so không phân biệt hoa thường.
        private static readonly string[] DanhSachYeu =
        {
            "admin", "password", "matkhau", "12345678", "123456789", "1234567890",
            "11111111", "00000000", "qwertyui", "abcd1234"
        };

        // Trả thông báo lỗi tiếng Việt hoặc null khi mật khẩu hợp lệ.
        public static string? Validate(string? matKhau, string? tenDangNhap, string? soDienThoai)
        {
            if (string.IsNullOrEmpty(matKhau) || matKhau.Length < DoDaiToiThieu || matKhau.Length > DoDaiToiDa)
            {
                return $"Mật khẩu phải có từ {DoDaiToiThieu} đến {DoDaiToiDa} ký tự.";
            }

            if (string.IsNullOrWhiteSpace(matKhau))
            {
                return "Mật khẩu không được chỉ gồm khoảng trắng.";
            }

            if (TrungTenDangNhap(matKhau, tenDangNhap))
            {
                return "Mật khẩu không được trùng tên đăng nhập.";
            }

            if (TrungSoDienThoai(matKhau, soDienThoai))
            {
                return "Mật khẩu không được trùng số điện thoại.";
            }

            if (DanhSachYeu.Any(x => string.Equals(x, matKhau, StringComparison.OrdinalIgnoreCase)))
            {
                return "Mật khẩu quá dễ đoán. Vui lòng chọn mật khẩu khác.";
            }

            return null;
        }

        // Mật khẩu vừa đăng nhập đúng là mật khẩu mặc định hoặc do người khác cấp (bảng spec §2.2).
        // soDienThoai chỉ truyền cho khách thuê.
        public static bool LaMatKhauMacDinh(string matKhau, string tenDangNhap, string? soDienThoai)
        {
            if (string.IsNullOrEmpty(matKhau))
            {
                return false;
            }

            return string.Equals(matKhau, MatKhauMacDinhAdmin, StringComparison.OrdinalIgnoreCase)
                || TrungTenDangNhap(matKhau, tenDangNhap)
                || TrungSoDienThoai(matKhau, soDienThoai);
        }

        // Chỉ giữ chữ số; đầu "84" đổi thành "0". Không còn chữ số nào thì trả null.
        public static string? ChuanHoaSoDienThoai(string? soDienThoai)
        {
            if (string.IsNullOrEmpty(soDienThoai))
            {
                return null;
            }

            var chuSo = new StringBuilder(soDienThoai.Length);
            foreach (var c in soDienThoai)
            {
                if (c >= '0' && c <= '9')
                {
                    chuSo.Append(c);
                }
            }

            if (chuSo.Length == 0)
            {
                return null;
            }

            var ketQua = chuSo.ToString();
            return ketQua.StartsWith("84", StringComparison.Ordinal) ? "0" + ketQua.Substring(2) : ketQua;
        }

        private static bool TrungTenDangNhap(string matKhau, string? tenDangNhap)
        {
            return !string.IsNullOrWhiteSpace(tenDangNhap)
                && string.Equals(matKhau.Trim(), tenDangNhap.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        private static bool TrungSoDienThoai(string matKhau, string? soDienThoai)
        {
            if (string.IsNullOrWhiteSpace(soDienThoai))
            {
                return false;
            }

            if (string.Equals(matKhau.Trim(), soDienThoai.Trim(), StringComparison.Ordinal))
            {
                return true;
            }

            // Chỉ so dạng chuẩn hóa khi mật khẩu trông như một số điện thoại (chữ số và dấu ngăn cách),
            // để mật khẩu chứa số điện thoại lẫn chữ cái không bị coi là trùng.
            if (!matKhau.All(c => (c >= '0' && c <= '9') || c == ' ' || c == '+' || c == '-' || c == '.' || c == '(' || c == ')'))
            {
                return false;
            }

            var mk = ChuanHoaSoDienThoai(matKhau);
            var sdt = ChuanHoaSoDienThoai(soDienThoai);
            return mk != null && sdt != null && string.Equals(mk, sdt, StringComparison.Ordinal);
        }
    }
}
