using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace QuanLyChoThuePhongTroWeb.Helpers
{
    public class DateTimeJsonConverter : JsonConverter<DateTime>
    {
        private const string Format = "dd/MM/yyyy HH:mm";

        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            string? value = reader.GetString();
            if (string.IsNullOrWhiteSpace(value))
            {
                return DateTime.MinValue;
            }

            // Thử parse theo các định dạng phổ biến
            if (DateTime.TryParseExact(value, Format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            {
                // Giả định client gửi lên giờ Việt Nam (GMT+7), cần quy đổi về UTC (trừ đi 7 giờ) để lưu DB
                return DateTime.SpecifyKind(dt.AddHours(-7), DateTimeKind.Utc);
            }

            if (DateTime.TryParseExact(value, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateOnly))
            {
                // Giả định client gửi lên ngày Việt Nam (GMT+7), quy đổi về UTC
                return DateTime.SpecifyKind(dateOnly.AddHours(-7), DateTimeKind.Utc);
            }

            if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var standardDt))
            {
                return DateTime.SpecifyKind(standardDt, DateTimeKind.Utc);
            }

            return DateTime.SpecifyKind(DateTime.Parse(value), DateTimeKind.Utc);
        }

        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        {
            // Nếu là UTC, quy đổi sang GMT+7 (giờ Việt Nam) trước khi hiển thị
            DateTime localDateTime = value.Kind == DateTimeKind.Utc ? value.AddHours(7) : value;
            writer.WriteStringValue(localDateTime.ToString(Format, CultureInfo.InvariantCulture));
        }
    }
}
