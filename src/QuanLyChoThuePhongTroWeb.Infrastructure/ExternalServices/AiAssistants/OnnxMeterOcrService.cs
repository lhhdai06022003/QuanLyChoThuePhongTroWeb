using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Services;
using QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.DTOs;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.ExternalServices.AiAssistants
{
    /// <summary>
    /// Adapter tích hợp model AI ONNX cục bộ vào luồng IMeterOcrService của hệ thống.
    /// Chỉ dùng model ONNX (nhanh, offline); không còn dự phòng sang dịch vụ ngoài.
    /// </summary>
    public class OnnxMeterOcrService : IMeterOcrService
    {
        private readonly IMeterScannerService _meterScannerService;
        private readonly ILogger<OnnxMeterOcrService> _logger;

        public OnnxMeterOcrService(
            IMeterScannerService meterScannerService,
            ILogger<OnnxMeterOcrService> logger)
        {
            _meterScannerService = meterScannerService ?? throw new ArgumentNullException(nameof(meterScannerService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<MeterOcrResult> ProcessImageAsync(
            byte[] imageBytes,
            string contentType,
            LoaiDongHo loaiDongHo,
            CancellationToken cancellationToken = default)
        {
            if (imageBytes == null || imageBytes.Length == 0)
            {
                return MeterOcrResult.Failed("Dữ liệu ảnh trống.");
            }

            cancellationToken.ThrowIfCancellationRequested();

            bool yeuCauDongHoDien = loaiDongHo == LoaiDongHo.Dien;

            try
            {
                using var stream = new MemoryStream(imageBytes);
                var scanResult = await _meterScannerService.QuetAnhDongHoAsync(stream, yeuCauDongHoDien, cancellationToken);

                // 1. Lỗi hệ thống (model chưa sẵn sàng)
                if (scanResult.LoiHeThong)
                {
                    _logger.LogError("Model nhận diện đồng hồ chưa sẵn sàng: {Message}", scanResult.ThongBaoLoi);
                    return MeterOcrResult.Failed(scanResult.ThongBaoLoi ?? "Model nhận diện đồng hồ chưa sẵn sàng.");
                }

                // 2. Chụp nhầm loại đồng hồ (ví dụ chốt điện mà chụp nước)
                if (scanResult.ChupNhamLoaiDongHo)
                {
                    _logger.LogWarning("Phát hiện chụp nhầm loại đồng hồ: {Message}", scanResult.ThongBaoLoi);
                    return MeterOcrResult.Unreadable(scanResult.ThongBaoLoi);
                }

                // 3. Nhận diện thành công chỉ số
                if (scanResult.DocThanhCong && scanResult.ChiSoDocDuoc.HasValue)
                {
                    var roundedValue = Math.Round(scanResult.ChiSoDocDuoc.Value, 3);
                    var confidence = Math.Clamp(scanResult.DoTinCayThapNhat, 0.0, 1.0);

                    _logger.LogInformation("ONNX Scanner nhận diện thành công: {Value} (Độ tin cậy: {Confidence}, Quét kép: {DualPass})",
                        roundedValue, confidence, scanResult.DaDungQuetKepTangCuong);

                    return MeterOcrResult.Readable(roundedValue, confidence);
                }

                return MeterOcrResult.Unreadable(scanResult.ThongBaoLoi ?? "Không thể nhận diện chỉ số từ ảnh đồng hồ.");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi ngoại lệ khi quét chỉ số qua ONNX Scanner.");
                return MeterOcrResult.Failed("Lỗi trong quá trình nhận diện hình ảnh.");
            }
        }
    }
}
