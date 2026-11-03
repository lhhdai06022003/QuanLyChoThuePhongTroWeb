using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Services;
using QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.DTOs;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using QuanLyChoThuePhongTroWeb.Infrastructure.ExternalServices.AiAssistants;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;
using Xunit.Abstractions;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.ExternalServices
{
    public class OnnxMeterScannerServiceTests
    {
        private readonly ITestOutputHelper _output;

        public OnnxMeterScannerServiceTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void YoloMeterScannerService_InitializesSuccessfully_WhenModelExists()
        {
            // Arrange & Act
            using var service = new YoloMeterScannerService(NullLogger<YoloMeterScannerService>.Instance);

            // Assert
            Assert.NotNull(service);
        }

        [Fact]
        public async Task QuetAnhDongHoAsync_ReturnsError_WhenStreamIsNull()
        {
            // Arrange
            using var service = new YoloMeterScannerService(NullLogger<YoloMeterScannerService>.Instance);

            // Act
            var result = await service.QuetAnhDongHoAsync(null!, yeuCauDongHoDien: true);

            // Assert
            Assert.False(result.DocThanhCong);
            Assert.Contains("không hợp lệ", result.ThongBaoLoi);
        }

        [Fact]
        public async Task OnnxMeterOcrService_ReturnsFailed_WhenImageBytesAreEmpty()
        {
            // Arrange
            using var scanner = new YoloMeterScannerService(NullLogger<YoloMeterScannerService>.Instance);
            var ocrService = new OnnxMeterOcrService(scanner, NullLogger<OnnxMeterOcrService>.Instance);

            // Act
            var result = await ocrService.ProcessImageAsync(Array.Empty<byte>(), "image/jpeg", LoaiDongHo.Dien);

            // Assert
            Assert.Equal(MeterOcrStatus.Failed, result.Status);
            Assert.Contains("trống", result.ErrorMessage);
        }

        [Fact]
        public async Task QuetAnhDongHoAsync_RunsInferenceOnGeneratedImage_WithoutCrashing()
        {
            // Arrange
            using var scanner = new YoloMeterScannerService(NullLogger<YoloMeterScannerService>.Instance);

            // Tạo ảnh dummy kích thước 640x640 màu xám (mặt đồng hồ trống)
            using var img = new Image<Rgb24>(640, 640, new Rgb24(128, 128, 128));
            using var ms = new MemoryStream();
            await img.SaveAsJpegAsync(ms);
            ms.Position = 0;

            // Act
            var result = await scanner.QuetAnhDongHoAsync(ms, yeuCauDongHoDien: true);

            // Assert - Ảnh trống không có khung đồng hồ nên bị từ chối theo đúng lý do, không đoán số
            Assert.NotNull(result);
            Assert.False(result.DocThanhCong);
            Assert.False(result.LoiHeThong);
            Assert.Equal("Không nhận ra mặt đồng hồ trong ảnh", result.ThongBaoLoi);
        }

        [Fact]
        public async Task OversizedImage_IsRejectedBeforeDecoding_AsUnreadable()
        {
            // Arrange: PNG một màu nén rất nhỏ nhưng có 40.005.625 điểm ảnh, vượt giới hạn 40 triệu.
            using var scanner = new YoloMeterScannerService(NullLogger<YoloMeterScannerService>.Instance);
            var ocrService = new OnnxMeterOcrService(scanner, NullLogger<OnnxMeterOcrService>.Instance);
            using var img = new Image<L8>(6325, 6325);
            using var ms = new MemoryStream();
            await img.SaveAsPngAsync(ms);
            Assert.True(ms.Length < 5 * 1024 * 1024);

            // Act
            ms.Position = 0;
            var scan = await scanner.QuetAnhDongHoAsync(ms, yeuCauDongHoDien: true);
            var ocr = await ocrService.ProcessImageAsync(ms.ToArray(), "image/png", LoaiDongHo.Dien);

            // Assert
            Assert.False(scan.DocThanhCong);
            Assert.False(scan.LoiHeThong);
            Assert.Equal("Ảnh có kích thước quá lớn. Vui lòng chụp lại hoặc giảm kích thước ảnh.", scan.ThongBaoLoi);
            Assert.Equal(MeterOcrStatus.Unreadable, ocr.Status);
        }

        [Fact]
        public async Task ImageAtPixelLimit_IsNotRejectedAsOversized()
        {
            // Arrange: đúng 40.000.000 điểm ảnh vẫn được giải mã.
            using var scanner = new YoloMeterScannerService(NullLogger<YoloMeterScannerService>.Instance);
            using var img = new Image<L8>(8000, 5000);
            using var ms = new MemoryStream();
            await img.SaveAsPngAsync(ms);
            ms.Position = 0;

            // Act
            var result = await scanner.QuetAnhDongHoAsync(ms, yeuCauDongHoDien: true);

            // Assert
            Assert.DoesNotContain("quá lớn", result.ThongBaoLoi ?? string.Empty);
        }

        [Fact]
        public async Task NonSeekableStream_IsBuffered_AndScanned()
        {
            // Arrange
            using var scanner = new YoloMeterScannerService(NullLogger<YoloMeterScannerService>.Instance);
            using var img = new Image<Rgb24>(640, 640, new Rgb24(128, 128, 128));
            using var ms = new MemoryStream();
            await img.SaveAsJpegAsync(ms);
            using var nonSeekable = new NonSeekableStream(ms.ToArray());

            // Act
            var result = await scanner.QuetAnhDongHoAsync(nonSeekable, yeuCauDongHoDien: true);

            // Assert: cùng kết luận với luồng thường (ảnh trống không có đồng hồ).
            ms.Position = 0;
            var expected = await scanner.QuetAnhDongHoAsync(ms, yeuCauDongHoDien: true);
            Assert.Equal(expected.ThongBaoLoi, result.ThongBaoLoi);
        }

        private sealed class NonSeekableStream : MemoryStream
        {
            public NonSeekableStream(byte[] data) : base(data) { }

            public override bool CanSeek => false;
        }

        [Fact]
        public async Task OnnxMeterOcrService_AdaptsUnreadableResult_WhenNoDigitsDetected()
        {
            // Arrange
            using var scanner = new YoloMeterScannerService(NullLogger<YoloMeterScannerService>.Instance);
            var ocrService = new OnnxMeterOcrService(scanner, NullLogger<OnnxMeterOcrService>.Instance);

            using var img = new Image<Rgb24>(640, 640, new Rgb24(100, 100, 100));
            using var ms = new MemoryStream();
            await img.SaveAsJpegAsync(ms);
            var bytes = ms.ToArray();

            // Act
            var result = await ocrService.ProcessImageAsync(bytes, "image/jpeg", LoaiDongHo.Dien);

            // Assert
            Assert.Equal(MeterOcrStatus.Unreadable, result.Status);
            Assert.NotNull(result.ErrorMessage);
        }

        [Fact]
        public async Task OnnxMeterOcrService_ReturnsFailed_WhenImageCannotBeDecoded()
        {
            using var scanner = new YoloMeterScannerService(NullLogger<YoloMeterScannerService>.Instance);
            var ocrService = new OnnxMeterOcrService(scanner, NullLogger<OnnxMeterOcrService>.Instance);

            var result = await ocrService.ProcessImageAsync(new byte[] { 1, 2, 3, 4, 5 }, "image/jpeg", LoaiDongHo.Dien);

            Assert.Equal(MeterOcrStatus.Failed, result.Status);
        }

        [Fact]
        public async Task OnnxMeterOcrService_ReturnsFailed_WhenScannerReportsSystemError()
        {
            var ocrService = new OnnxMeterOcrService(
                new StubScanner(new MeterScanResultDto { DocThanhCong = false, LoiHeThong = true, ThongBaoLoi = "Model chưa sẵn sàng" }),
                NullLogger<OnnxMeterOcrService>.Instance);

            var result = await ocrService.ProcessImageAsync(new byte[] { 1 }, "image/jpeg", LoaiDongHo.Dien);

            Assert.Equal(MeterOcrStatus.Failed, result.Status);
        }

        [Fact]
        public async Task OnnxMeterOcrService_ReturnsUnreadableWithMessage_WhenWrongMeterType()
        {
            var ocrService = new OnnxMeterOcrService(
                new StubScanner(new MeterScanResultDto { DocThanhCong = false, ChupNhamLoaiDongHo = true, ThongBaoLoi = "Ảnh tải lên là Đồng hồ Nước nhưng bạn đang chốt số Điện." }),
                NullLogger<OnnxMeterOcrService>.Instance);

            var result = await ocrService.ProcessImageAsync(new byte[] { 1 }, "image/jpeg", LoaiDongHo.Dien);

            Assert.Equal(MeterOcrStatus.Unreadable, result.Status);
            Assert.Equal("Ảnh tải lên là Đồng hồ Nước nhưng bạn đang chốt số Điện.", result.ErrorMessage);
        }

        [Fact]
        public async Task OnnxMeterOcrService_ReturnsReadable_WhenScannerReadsValue()
        {
            var ocrService = new OnnxMeterOcrService(
                new StubScanner(new MeterScanResultDto { DocThanhCong = true, ChiSoDocDuoc = 1234.567m, DoTinCayThapNhat = 0.93 }),
                NullLogger<OnnxMeterOcrService>.Instance);

            var result = await ocrService.ProcessImageAsync(new byte[] { 1 }, "image/jpeg", LoaiDongHo.Dien);

            Assert.Equal(MeterOcrStatus.Readable, result.Status);
            Assert.Equal(1234.567m, result.SuggestedValue);
            Assert.Equal(0.93, result.Confidence);
        }

        private sealed class StubScanner : IMeterScannerService
        {
            private readonly MeterScanResultDto _result;

            public StubScanner(MeterScanResultDto result)
            {
                _result = result;
            }

            public Task<MeterScanResultDto> QuetAnhDongHoAsync(Stream imageStream, bool yeuCauDongHoDien, CancellationToken cancellationToken = default)
                => Task.FromResult(_result);
        }
    }
}
