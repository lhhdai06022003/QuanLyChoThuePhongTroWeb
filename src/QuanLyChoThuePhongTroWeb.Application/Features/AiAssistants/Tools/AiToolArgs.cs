using System;
using System.Globalization;
using System.Text.Json;

namespace QuanLyChoThuePhongTroWeb.Application.Features.AiAssistants.Tools
{
    // Lỗi tham số do model truyền sai; thông điệp tiếng Việt được trả lại cho model để hỏi lại người dùng.
    public sealed class AiToolArgException : Exception
    {
        public AiToolArgException(string message) : base(message) { }
    }

    public sealed class AiToolArgs
    {
        private readonly JsonElement _root;

        private AiToolArgs(JsonElement root) { _root = root; }

        public static AiToolArgs Parse(string? argumentsJson)
        {
            try
            {
                using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(argumentsJson) ? "{}" : argumentsJson);
                if (doc.RootElement.ValueKind != JsonValueKind.Object)
                {
                    throw new AiToolArgException("Tham số không hợp lệ.");
                }
                return new AiToolArgs(doc.RootElement.Clone());
            }
            catch (JsonException)
            {
                throw new AiToolArgException("Tham số không hợp lệ.");
            }
        }

        // Số nguyên: nhận JSON number có giá trị nguyên hoặc chuỗi số. Thiếu, null hoặc chuỗi rỗng = null.
        public int? GetInt(string name, string errorMessage, bool zeroMeansMissing = false)
        {
            if (!_root.TryGetProperty(name, out var el) || el.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            long value;
            if (el.ValueKind == JsonValueKind.Number)
            {
                if (!el.TryGetDecimal(out var d) || d != decimal.Truncate(d) || d > int.MaxValue || d < int.MinValue)
                {
                    throw new AiToolArgException(errorMessage);
                }
                value = (long)d;
            }
            else if (el.ValueKind == JsonValueKind.String)
            {
                var s = el.GetString();
                if (string.IsNullOrWhiteSpace(s)) return null;
                if (!int.TryParse(s.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
                {
                    throw new AiToolArgException(errorMessage);
                }
                value = parsed;
            }
            else
            {
                throw new AiToolArgException(errorMessage);
            }

            if (zeroMeansMissing && value == 0) return null;
            return (int)value;
        }

        public decimal? GetDecimal(string name, string errorMessage)
        {
            if (!_root.TryGetProperty(name, out var el) || el.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            if (el.ValueKind == JsonValueKind.Number)
            {
                if (!el.TryGetDecimal(out var d)) throw new AiToolArgException(errorMessage);
                return d;
            }
            if (el.ValueKind == JsonValueKind.String)
            {
                var s = el.GetString();
                if (string.IsNullOrWhiteSpace(s)) return null;
                if (!decimal.TryParse(s.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
                {
                    throw new AiToolArgException(errorMessage);
                }
                return parsed;
            }
            throw new AiToolArgException(errorMessage);
        }

        public string? GetString(string name)
        {
            if (!_root.TryGetProperty(name, out var el)) return null;
            return el.ValueKind switch
            {
                JsonValueKind.String => el.GetString(),
                JsonValueKind.Number => el.GetRawText(),
                _ => null
            };
        }

        public DateOnly? GetDate(string name, string errorMessage)
        {
            var s = GetString(name);
            if (string.IsNullOrWhiteSpace(s)) return null;
            if (!DateOnly.TryParseExact(s.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                throw new AiToolArgException(errorMessage);
            }
            return date;
        }
    }
}
