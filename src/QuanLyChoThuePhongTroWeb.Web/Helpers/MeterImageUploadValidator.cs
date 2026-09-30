using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using QuanLyChoThuePhongTroWeb.Application.Common.Configurations;
using QuanLyChoThuePhongTroWeb.Application.Common.Files;

namespace QuanLyChoThuePhongTroWeb.Web.Helpers
{
    public static class MeterImageUploadValidator
    {
        public static async Task<(UploadFile? File, string? Error)> ValidateAsync(
            IFormFile? file,
            MeterImageOptions options,
            CancellationToken ct = default)
        {
            if (file == null || file.Length == 0)
            {
                return (null, "Vui lòng chọn ảnh đồng hồ.");
            }

            if (file.Length > options.MaxFileSizeBytes)
            {
                var mb = options.MaxFileSizeBytes / (1024 * 1024);
                return (null, $"Ảnh vượt quá dung lượng cho phép ({mb} MB).");
            }

            var declaredContentType = (file.ContentType ?? string.Empty).Trim().ToLowerInvariant();
            if (declaredContentType == "image/jpg")
            {
                declaredContentType = "image/jpeg";
            }

            if (!options.AllowedContentTypes.Contains(declaredContentType))
            {
                return (null, "Định dạng tệp không được hỗ trợ. Chỉ chấp nhận JPG, PNG, WEBP.");
            }

            var ms = new MemoryStream();
            await file.CopyToAsync(ms, ct);

            if (ms.Length < 3)
            {
                ms.Dispose();
                return (null, "Nội dung tệp không khớp định dạng ảnh.");
            }

            var buffer = ms.GetBuffer();
            string? detectedContentType = null;

            // JPEG: FF D8 FF
            if (buffer.Length >= 3 && buffer[0] == 0xFF && buffer[1] == 0xD8 && buffer[2] == 0xFF)
            {
                detectedContentType = "image/jpeg";
            }
            // PNG: 89 50 4E 47 0D 0A 1A 0A
            else if (buffer.Length >= 8 &&
                     buffer[0] == 0x89 && buffer[1] == 0x50 && buffer[2] == 0x4E && buffer[3] == 0x47 &&
                     buffer[4] == 0x0D && buffer[5] == 0x0A && buffer[6] == 0x1A && buffer[7] == 0x0A)
            {
                detectedContentType = "image/png";
            }
            // WebP: 52 49 46 46 (RIFF) at 0..3 and 57 45 42 50 (WEBP) at 8..11
            else if (buffer.Length >= 12 &&
                     buffer[0] == 0x52 && buffer[1] == 0x49 && buffer[2] == 0x46 && buffer[3] == 0x46 &&
                     buffer[8] == 0x57 && buffer[9] == 0x45 && buffer[10] == 0x42 && buffer[11] == 0x50)
            {
                detectedContentType = "image/webp";
            }

            if (detectedContentType == null || detectedContentType != declaredContentType)
            {
                ms.Dispose();
                return (null, "Nội dung tệp không khớp định dạng ảnh.");
            }

            ms.Position = 0;
            var fileName = Path.GetFileName(file.FileName);
            var uploadFile = new UploadFile(ms, fileName, declaredContentType, ms.Length);
            return (uploadFile, null);
        }
    }
}
