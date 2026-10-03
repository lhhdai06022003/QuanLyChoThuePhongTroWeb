using System;
using System.Globalization;

namespace QuanLyChoThuePhongTroWeb.Web.Helpers
{
    // Giờ người dùng nhập trên form là giờ Việt Nam (UTC+7, không có giờ mùa hè); dữ liệu lưu UTC.
    public static class VietnamTime
    {
        private static readonly string[] Formats =
        {
            "yyyy-MM-dd'T'HH:mm",
            "yyyy-MM-dd'T'HH:mm:ss",
            "yyyy-MM-dd HH:mm",
            "dd/MM/yyyy HH:mm",
            "yyyy-MM-dd"
        };

        public static bool TryParseToUtc(string? value, out DateTime utc)
        {
            utc = default;
            if (string.IsNullOrWhiteSpace(value) ||
                !DateTime.TryParseExact(value.Trim(), Formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var local))
            {
                return false;
            }

            utc = DateTime.SpecifyKind(local.AddHours(-7), DateTimeKind.Utc);
            return true;
        }
    }
}
