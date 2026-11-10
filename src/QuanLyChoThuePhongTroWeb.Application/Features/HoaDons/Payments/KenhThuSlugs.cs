using System;

namespace QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Payments
{
    // Tên kênh thu dạng chữ dùng trên URL và bộ lọc của giao diện (không dùng số thứ tự enum).
    public static class KenhThuSlugs
    {
        public const string TienMat = "tien-mat";
        public const string ChuyenKhoan = "chuyen-khoan";
        public const string SePay = "sepay";
        public const string VnPay = "vnpay";
        public const string CanTruCoc = "can-tru-coc";

        // Bỏ khoảng trắng đầu cuối, không phân biệt hoa thường; rỗng hoặc không nhận ra thì false.
        public static bool TryParse(string? slug, out KenhThu kenh)
        {
            kenh = default;
            var s = slug?.Trim();
            if (string.IsNullOrEmpty(s))
            {
                return false;
            }

            if (s.Equals(TienMat, StringComparison.OrdinalIgnoreCase)) { kenh = KenhThu.TienMat; return true; }
            if (s.Equals(ChuyenKhoan, StringComparison.OrdinalIgnoreCase)) { kenh = KenhThu.ChuyenKhoan; return true; }
            if (s.Equals(SePay, StringComparison.OrdinalIgnoreCase)) { kenh = KenhThu.SePay; return true; }
            if (s.Equals(VnPay, StringComparison.OrdinalIgnoreCase)) { kenh = KenhThu.VnPay; return true; }
            if (s.Equals(CanTruCoc, StringComparison.OrdinalIgnoreCase)) { kenh = KenhThu.CanTruCoc; return true; }
            return false;
        }

        public static string Slug(KenhThu kenh) => kenh switch
        {
            KenhThu.TienMat => TienMat,
            KenhThu.ChuyenKhoan => ChuyenKhoan,
            KenhThu.SePay => SePay,
            KenhThu.VnPay => VnPay,
            KenhThu.CanTruCoc => CanTruCoc,
            _ => throw new ArgumentOutOfRangeException(nameof(kenh), kenh, null)
        };
    }
}
