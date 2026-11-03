using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace QuanLyChoThuePhongTroWeb.Application.Abstractions.Services
{
    /// <summary>
    /// Kết quả nhận diện chỉ số đồng hồ điện/nước từ AI Model (meter_scanner.onnx).
    /// </summary>
    public sealed record MeterScanResultDto
    {
        public bool DocThanhCong { get; init; }
        public decimal? ChiSoDocDuoc { get; init; }
        public string ChuoiSoTho { get; init; } = string.Empty;
        public double DoTinCayThapNhat { get; init; }
        public bool? PhatHienLaDongHoDien { get; init; }
        public bool ChupNhamLoaiDongHo { get; init; }
        public bool DaDungQuetKepTangCuong { get; init; }
        public string? ThongBaoLoi { get; init; }
        public bool LoiHeThong { get; init; }
    }

    /// <summary>
    /// Dịch vụ AI OCR đọc chỉ số đồng hồ điện/nước trực tiếp từ Stream bộ nhớ.
    /// </summary>
    public interface IMeterScannerService
    {
        /// <summary>
        /// Quét ảnh đồng hồ từ Stream trong RAM (không cần lưu file tạm ra đĩa).
        /// </summary>
        /// <param name="imageStream">Stream ảnh chụp mặt đồng hồ.</param>
        /// <param name="yeuCauDongHoDien">True nếu đang chốt số Điện, False nếu đang chốt số Nước.</param>
        /// <param name="cancellationToken">Token hủy tác vụ.</param>
        Task<MeterScanResultDto> QuetAnhDongHoAsync(
            Stream imageStream,
            bool yeuCauDongHoDien,
            CancellationToken cancellationToken = default);
    }
}
