using System.Globalization;

namespace QuanLyChoThuePhongTroWeb.Application.Common.Formatting
{
    // Định dạng tiền dùng trên Dashboard và trang Việc cần làm: "3.590.000 đ". Không dùng culture vi-VN (phụ thuộc ICU).
    public static class TienTe
    {
        public static string DinhDang(decimal soTien)
            => string.Format(CultureInfo.InvariantCulture, "{0:#,##0}", soTien).Replace(",", ".") + " đ";
    }
}
