using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels.Responses
{
    // Dữ liệu trang Lịch sử thanh toán. Mọi tham số lọc đến từ URL đã được kiểm lại ở controller.
    public class LichSuThanhToanPageViewModel
    {
        public List<SelectListItem> ChiNhanhs { get; set; } = new();
        public bool LaAdmin { get; set; }
        public int ChiNhanhId { get; set; }
        // Slug kênh thu: tien-mat, chuyen-khoan, sepay, vnpay, can-tru-coc; rỗng = tất cả kênh.
        public string PhuongThuc { get; set; } = "";
        // "", "toi", "he-thong" hoặc NguoiDungId (chỉ Admin).
        public string NguoiThu { get; set; } = "";
        // Tên hiển thị khi NguoiThu là NguoiDungId.
        public string? TenNguoiThu { get; set; }
        // Kỳ hóa đơn: chỉ Admin.
        public int? KyThang { get; set; }
        public int? KyNam { get; set; }
        // yyyy-MM-dd; rỗng = không lọc khoảng ngày.
        public string TuNgay { get; set; } = "";
        public string DenNgay { get; set; } = "";
        public string BankId { get; set; } = "MB";
        public string AccountNumber { get; set; } = "";
        public string AccountName { get; set; } = "";

        public bool CoKy => KyThang.HasValue && KyNam.HasValue;

        // Giá trị cho <input type="month">.
        public string KyText => CoKy ? $"{KyNam!.Value:0000}-{KyThang!.Value:00}" : "";

        public static LichSuThanhToanPageViewModel From(
            IReadOnlyList<SelectOptionDto> chiNhanhs, bool laAdmin, int chiNhanhId, string phuongThuc, string nguoiThu, string? tenNguoiThu,
            int? kyThang, int? kyNam, string tuNgay, string denNgay, string bankId, string accountNumber, string accountName)
        {
            var selected = chiNhanhId.ToString(CultureInfo.InvariantCulture);
            return new LichSuThanhToanPageViewModel
            {
                ChiNhanhs = chiNhanhs.Select(c => new SelectListItem { Value = c.Value, Text = c.Text, Selected = c.Value == selected }).ToList(),
                LaAdmin = laAdmin,
                ChiNhanhId = chiNhanhId,
                PhuongThuc = phuongThuc,
                NguoiThu = nguoiThu,
                TenNguoiThu = tenNguoiThu,
                KyThang = kyThang,
                KyNam = kyNam,
                TuNgay = tuNgay,
                DenNgay = denNgay,
                BankId = bankId,
                AccountNumber = accountNumber,
                AccountName = accountName
            };
        }
    }
}
