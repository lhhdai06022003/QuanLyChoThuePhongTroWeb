using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels.Responses
{
    public class BaoCaoPageViewModel
    {
        public string Tab { get; set; } = "doanh-thu";
        public List<SelectListItem> ChiNhanhs { get; set; } = new();
        public int? ChiNhanhId { get; set; }
        // Nhãn của lựa chọn "mọi chi nhánh"; null khi nhân viên chỉ có đúng một chi nhánh.
        public string? NhanTatCa { get; set; }
        public bool ChuaPhanCong { get; set; }
        // Chỉ Admin thấy tab Doanh thu.
        public bool CoXemDoanhThu { get; set; }
        // Dạng yyyy-MM cho input type="month".
        public string TuKyMacDinh { get; set; } = "";
        public string DenKyMacDinh { get; set; } = "";

        public static BaoCaoPageViewModel From(string tab, IReadOnlyList<SelectOptionDto> chiNhanhs, int? chiNhanhId, bool isAdmin, int thangHienTai, int namHienTai,
            (int TuThang, int TuNam, int DenThang, int DenNam)? khoang = null)
        {
            var model = new BaoCaoPageViewModel { Tab = tab, CoXemDoanhThu = isAdmin };

            var hopLe = chiNhanhId.HasValue && chiNhanhs.Any(c => c.Value == chiNhanhId.Value.ToString(CultureInfo.InvariantCulture));
            int? chon = hopLe ? chiNhanhId : null;

            if (isAdmin)
            {
                model.NhanTatCa = "Tất cả chi nhánh";
            }
            else if (chiNhanhs.Count == 0)
            {
                model.ChuaPhanCong = true;
            }
            else if (chiNhanhs.Count == 1)
            {
                chon = int.Parse(chiNhanhs[0].Value, CultureInfo.InvariantCulture);
            }
            else
            {
                model.NhanTatCa = "Tất cả chi nhánh của tôi";
            }

            model.ChiNhanhId = chon;
            var selected = chon?.ToString(CultureInfo.InvariantCulture);
            foreach (var c in chiNhanhs)
            {
                model.ChiNhanhs.Add(new SelectListItem { Value = c.Value, Text = c.Text, Selected = c.Value == selected });
            }

            // Mặc định 12 kỳ gần nhất, kết thúc ở tháng hiện tại theo giờ Việt Nam.
            // hoặc đúng khoảng kỳ truyền vào từ URL (đã kiểm hợp lệ).
            var den = khoang.HasValue ? new System.DateTime(khoang.Value.DenNam, khoang.Value.DenThang, 1) : new System.DateTime(namHienTai, thangHienTai, 1);
            var tu = khoang.HasValue ? new System.DateTime(khoang.Value.TuNam, khoang.Value.TuThang, 1) : den.AddMonths(-11);
            model.DenKyMacDinh = den.ToString("yyyy-MM", CultureInfo.InvariantCulture);
            model.TuKyMacDinh = tu.ToString("yyyy-MM", CultureInfo.InvariantCulture);
            return model;
        }
    }
}
