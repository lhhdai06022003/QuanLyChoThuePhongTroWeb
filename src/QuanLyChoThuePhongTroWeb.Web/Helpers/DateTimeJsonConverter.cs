using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace QuanLyChoThuePhongTroWeb.Helpers
{
    public class DateTimeJsonConverter : JsonConverter<DateTime>
    {
        private const string IsoFormat = "yyyy-MM-dd'T'HH:mm:ss";
        private static readonly string[] ReadFormats = new[]
        {
            "yyyy-MM-dd'T'HH:mm:ss",
            "yyyy-MM-dd'T'HH:mm:ss.fff",
            "yyyy-MM-dd",
            "dd/MM/yyyy HH:mm",
            "dd/MM/yyyy"
        };

        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            string? value = reader.GetString();
            if (string.IsNullOrWhiteSpace(value))
            {
                return DateTime.MinValue;
            }

            if (DateTime.TryParseExact(value, ReadFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            {
                bool isIso = value.Contains('T') || value.Contains('-');
                if (isIso)
                {
                    return DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
                }
                return DateTime.SpecifyKind(parsed.AddHours(-7), DateTimeKind.Utc);
            }

            if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var standardDt))
            {
                return DateTime.SpecifyKind(standardDt, DateTimeKind.Utc);
            }

            return DateTime.SpecifyKind(DateTime.Parse(value), DateTimeKind.Utc);
        }

        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        {
            DateTime localDateTime = value.Kind == DateTimeKind.Utc ? value.AddHours(7) : value;
            writer.WriteStringValue(localDateTime.ToString(IsoFormat, CultureInfo.InvariantCulture));
        }
    }
}
