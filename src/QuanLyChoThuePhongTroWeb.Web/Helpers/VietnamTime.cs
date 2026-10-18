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

        // Hạn còn hiệu lực (sau thời điểm hiện tại) thì trả giờ Việt Nam "dd/MM/yyyy HH:mm", ngược lại chuỗi rỗng.
        public static string FormatDeadline(DateTime? utc, DateTime nowUtc)
            => utc.HasValue && utc.Value > nowUtc
                ? utc.Value.AddHours(7).ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture)
                : string.Empty;

        // Đổi thời điểm UTC sang giờ Việt Nam rồi định dạng theo format cho trước (ví dụ "dd/MM/yyyy HH:mm").
        public static string Format(DateTime utc, string format)
            => utc.AddHours(7).ToString(format, CultureInfo.InvariantCulture);

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

        // Như TryParseToUtc nhưng chỉ nhận đúng một định dạng, cho dữ liệu máy gửi (ví dụ webhook SePay: "yyyy-MM-dd HH:mm:ss").
        public static bool TryParseExactToUtc(string? value, string format, out DateTime utc)
        {
            utc = default;
            if (string.IsNullOrWhiteSpace(value) ||
                !DateTime.TryParseExact(value.Trim(), format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var local) ||
                local < DateTime.MinValue.AddHours(7))
            {
                return false;
            }

            utc = DateTime.SpecifyKind(local.AddHours(-7), DateTimeKind.Utc);
            return true;
        }
    }
}
