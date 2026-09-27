using System;

namespace QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.DTOs
{
    public enum MeterOcrStatus
    {
        Readable,
        Unreadable,
        Failed
    }

    public class MeterOcrResult
    {
        public MeterOcrStatus Status { get; }
        public decimal? SuggestedValue { get; }
        public double? Confidence { get; }
        public string? ErrorMessage { get; }

        private MeterOcrResult(MeterOcrStatus status, decimal? suggestedValue, double? confidence, string? errorMessage)
        {
            Status = status;
            SuggestedValue = suggestedValue;
            Confidence = confidence;
            ErrorMessage = errorMessage;
        }

        public static MeterOcrResult Readable(decimal suggestedValue, double? confidence = null)
        {
            if (suggestedValue < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(suggestedValue), "Chỉ số gợi ý không được âm.");
            }

            var bits = decimal.GetBits(suggestedValue);
            var scale = (bits[3] >> 16) & 0x7F;
            if (scale > 3)
            {
                throw new ArgumentException("Chỉ số gợi ý không được có quá 3 chữ số phần thập phân.", nameof(suggestedValue));
            }

            if (confidence.HasValue && (confidence.Value < 0 || confidence.Value > 1))
            {
                throw new ArgumentOutOfRangeException(nameof(confidence), "Độ tin cậy phải nằm trong khoảng từ 0 đến 1.");
            }

            return new MeterOcrResult(MeterOcrStatus.Readable, suggestedValue, confidence, null);
        }

        public static MeterOcrResult Unreadable(string? reason = null)
        {
            return new MeterOcrResult(MeterOcrStatus.Unreadable, null, null, reason);
        }

        public static MeterOcrResult Failed(string errorMessage)
        {
            if (string.IsNullOrWhiteSpace(errorMessage))
            {
                throw new ArgumentException("Thông báo lỗi không được để trống.", nameof(errorMessage));
            }

            return new MeterOcrResult(MeterOcrStatus.Failed, null, null, errorMessage);
        }
    }
}
