using System;
using System.Collections.Generic;
using System.IO;

namespace QuanLyChoThuePhongTroWeb.Application.Common.Files
{
    // Kiểm ảnh minh chứng chuyển khoản: đuôi tệp, content-type và magic bytes phải khớp nhau (spec §12, §26).
    // Web gọi trước khi chuyển IFormFile sang UploadFile, service gọi lại lần nữa.
    public static class ProofImageValidator
    {
        private static readonly Dictionary<string, string> ExtensionToContentType = new(StringComparer.OrdinalIgnoreCase)
        {
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".png"] = "image/png",
            [".webp"] = "image/webp"
        };

        public static string? Validate(UploadFile? file, long maxBytes)
        {
            if (file?.Content == null || file.Length <= 0)
            {
                return "Vui lòng chọn ảnh minh chứng.";
            }

            if (file.Length > maxBytes)
            {
                return $"Ảnh vượt quá dung lượng cho phép ({maxBytes / (1024 * 1024)} MB).";
            }

            if (!ExtensionToContentType.TryGetValue(Path.GetExtension(file.FileName ?? string.Empty), out var expectedType))
            {
                return "Chỉ chấp nhận ảnh JPG, PNG hoặc WEBP.";
            }

            if (NormalizeContentType(file.ContentType) != expectedType)
            {
                return "Định dạng ảnh không khớp với đuôi tệp.";
            }

            if (!file.Content.CanSeek || !file.Content.CanRead)
            {
                return "Không đọc được nội dung ảnh.";
            }

            var start = file.Content.Position;
            Span<byte> header = stackalloc byte[12];
            var read = file.Content.Read(header);
            file.Content.Position = start;

            return DetectContentType(header[..read]) == expectedType
                ? null
                : "Nội dung tệp không khớp định dạng ảnh.";
        }

        public static string NormalizeContentType(string? contentType)
        {
            var normalized = (contentType ?? string.Empty).Trim().ToLowerInvariant();
            return normalized == "image/jpg" ? "image/jpeg" : normalized;
        }

        public static string? DetectContentType(ReadOnlySpan<byte> header)
        {
            if (header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
            {
                return "image/jpeg";
            }

            if (header.Length >= 8 && header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47 &&
                header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A)
            {
                return "image/png";
            }

            if (header.Length >= 12 && header[0] == (byte)'R' && header[1] == (byte)'I' && header[2] == (byte)'F' && header[3] == (byte)'F' &&
                header[8] == (byte)'W' && header[9] == (byte)'E' && header[10] == (byte)'B' && header[11] == (byte)'P')
            {
                return "image/webp";
            }

            return null;
        }
    }
}
