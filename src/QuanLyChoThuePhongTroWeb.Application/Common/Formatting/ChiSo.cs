using System.Globalization;

namespace QuanLyChoThuePhongTroWeb.Application.Common.Formatting
{
    // Định dạng chỉ số đồng hồ kiểu Việt Nam, bỏ số 0 thập phân thừa: 100.000m -> "100", 1234.5m -> "1.234,5".
    // Chỉ số đọc từ DB có 3 chữ số thập phân, in thẳng ra sẽ thành "100.000" (dễ đọc nhầm là một trăm nghìn).
    // Không dùng culture vi-VN (phụ thuộc ICU), giống TienTe.
    public static class ChiSo
    {
        public static string DinhDang(decimal chiSo)
            => chiSo.ToString("#,##0.###", CultureInfo.InvariantCulture)
                .Replace(',', '\u0001')
                .Replace('.', ',')
                .Replace('\u0001', '.');
    }
}
