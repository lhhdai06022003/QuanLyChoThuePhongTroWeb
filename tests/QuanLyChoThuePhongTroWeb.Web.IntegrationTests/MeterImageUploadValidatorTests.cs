using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using QuanLyChoThuePhongTroWeb.Application.Common.Configurations;
using QuanLyChoThuePhongTroWeb.Web.Helpers;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Web.IntegrationTests
{
    public class MeterImageUploadValidatorTests
    {
        private readonly MeterImageOptions _options = new()
        {
            MaxFileSizeBytes = 5 * 1024 * 1024,
            AllowedContentTypes = new() { "image/jpeg", "image/png", "image/webp" }
        };

        private static IFormFile CreateFormFile(byte[] content, string fileName, string contentType)
        {
            var stream = new MemoryStream(content);
            return new FormFile(stream, 0, content.Length, "file", fileName)
            {
                Headers = new HeaderDictionary(),
                ContentType = contentType
            };
        }

        [Fact]
        public async Task ValidateAsync_ValidJpeg_Succeeds()
        {
            // Magic bytes FF D8 FF
            var bytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01 };
            var formFile = CreateFormFile(bytes, "meter.jpg", "image/jpeg");

            var (file, error) = await MeterImageUploadValidator.ValidateAsync(formFile, _options, CancellationToken.None);

            Assert.Null(error);
            Assert.NotNull(file);
            Assert.Equal("meter.jpg", file.FileName);
            Assert.Equal("image/jpeg", file.ContentType);
            Assert.Equal(bytes.Length, file.Length);
            Assert.Equal(0, file.Content.Position);
        }

        [Fact]
        public async Task ValidateAsync_ValidPng_Succeeds()
        {
            // Magic bytes 89 50 4E 47 0D 0A 1A 0A
            var bytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D };
            var formFile = CreateFormFile(bytes, "meter.png", "image/png");

            var (file, error) = await MeterImageUploadValidator.ValidateAsync(formFile, _options, CancellationToken.None);

            Assert.Null(error);
            Assert.NotNull(file);
            Assert.Equal("image/png", file.ContentType);
        }

        [Fact]
        public async Task ValidateAsync_ValidWebp_Succeeds()
        {
            // Magic bytes: 52 49 46 46 (RIFF) at 0..3, and 57 45 42 50 (WEBP) at 8..11
            var bytes = new byte[] {
                0x52, 0x49, 0x46, 0x46, // RIFF
                0x24, 0x00, 0x00, 0x00, // size
                0x57, 0x45, 0x42, 0x50  // WEBP
            };
            var formFile = CreateFormFile(bytes, "meter.webp", "image/webp");

            var (file, error) = await MeterImageUploadValidator.ValidateAsync(formFile, _options, CancellationToken.None);

            Assert.Null(error);
            Assert.NotNull(file);
            Assert.Equal("image/webp", file.ContentType);
        }

        [Fact]
        public async Task ValidateAsync_NullFile_ReturnsError()
        {
            var (file, error) = await MeterImageUploadValidator.ValidateAsync(null, _options, CancellationToken.None);

            Assert.Null(file);
            Assert.Equal("Vui lòng chọn ảnh đồng hồ.", error);
        }

        [Fact]
        public async Task ValidateAsync_EmptyFile_ReturnsError()
        {
            var formFile = CreateFormFile(System.Array.Empty<byte>(), "empty.jpg", "image/jpeg");

            var (file, error) = await MeterImageUploadValidator.ValidateAsync(formFile, _options, CancellationToken.None);

            Assert.Null(file);
            Assert.Equal("Vui lòng chọn ảnh đồng hồ.", error);
        }

        [Fact]
        public async Task ValidateAsync_ExceedsMaxSize_ReturnsError()
        {
            var lowLimitOptions = new MeterImageOptions
            {
                MaxFileSizeBytes = 16,
                AllowedContentTypes = new() { "image/jpeg" }
            };
            var bytes = new byte[32];
            var formFile = CreateFormFile(bytes, "large.jpg", "image/jpeg");

            var (file, error) = await MeterImageUploadValidator.ValidateAsync(formFile, lowLimitOptions, CancellationToken.None);

            Assert.Null(file);
            Assert.Equal($"Ảnh vượt quá dung lượng cho phép ({lowLimitOptions.MaxFileSizeBytes / (1024 * 1024)} MB).", error);
        }

        [Fact]
        public async Task ValidateAsync_UnsupportedContentType_Gif_ReturnsError()
        {
            var bytes = new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
            var formFile = CreateFormFile(bytes, "meter.gif", "image/gif");

            var (file, error) = await MeterImageUploadValidator.ValidateAsync(formFile, _options, CancellationToken.None);

            Assert.Null(file);
            Assert.Equal("Định dạng tệp không được hỗ trợ. Chỉ chấp nhận JPG, PNG, WEBP.", error);
        }

        [Fact]
        public async Task ValidateAsync_TxtFile_WithPngContentType_ReturnsMismatchError()
        {
            var bytes = Encoding.UTF8.GetBytes("This is plain text and not a png file.");
            var formFile = CreateFormFile(bytes, "meter.png", "image/png");

            var (file, error) = await MeterImageUploadValidator.ValidateAsync(formFile, _options, CancellationToken.None);

            Assert.Null(file);
            Assert.Equal("Nội dung tệp không khớp định dạng ảnh.", error);
        }

        [Fact]
        public async Task ValidateAsync_PngFile_DeclaredAsJpeg_ReturnsMismatchError()
        {
            // Valid PNG bytes declared as image/jpeg
            var bytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D };
            var formFile = CreateFormFile(bytes, "meter.jpg", "image/jpeg");

            var (file, error) = await MeterImageUploadValidator.ValidateAsync(formFile, _options, CancellationToken.None);

            Assert.Null(file);
            Assert.Equal("Nội dung tệp không khớp định dạng ảnh.", error);
        }

        [Fact]
        public async Task ValidateAsync_ImageJpg_AcceptedAsImageJpeg()
        {
            var bytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01 };
            var formFile = CreateFormFile(bytes, "meter.jpg", "image/jpg");

            var (file, error) = await MeterImageUploadValidator.ValidateAsync(formFile, _options, CancellationToken.None);

            Assert.Null(error);
            Assert.NotNull(file);
            Assert.Equal("image/jpeg", file.ContentType);
        }

        [Fact]
        public async Task ValidateAsync_OnlyTwoBytes_ReturnsMismatchError()
        {
            var bytes = new byte[] { 0xFF, 0xD8 };
            var formFile = CreateFormFile(bytes, "meter.jpg", "image/jpeg");

            var (file, error) = await MeterImageUploadValidator.ValidateAsync(formFile, _options, CancellationToken.None);

            Assert.Null(file);
            Assert.Equal("Nội dung tệp không khớp định dạng ảnh.", error);
        }
    }
}
