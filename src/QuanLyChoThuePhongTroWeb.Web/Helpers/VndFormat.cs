using System.Globalization;

namespace QuanLyChoThuePhongTroWeb.Web.Helpers
{
    // Định dạng tiền kiểu Việt Nam (1.000.000 ₫) không phụ thuộc culture của server;
    // ToString("N0") trần dùng culture hiện tại nên từng hiện "1,000,000".
    public static class VndFormat
    {
        private static readonly CultureInfo ViVn = CultureInfo.GetCultureInfo("vi-VN");

        public static string Number(decimal amount) => amount.ToString("N0", ViVn);

        public static string Money(decimal amount) => Number(amount) + " ₫";
    }
}
