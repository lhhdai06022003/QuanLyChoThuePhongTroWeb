using System;

namespace QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.DTOs
{
    public sealed class AdjustInvoiceRequest
    {
        public int HoaDonId { get; init; }
        public decimal ChiSoDienMoi { get; init; }
        public decimal ChiSoNuocMoi { get; init; }
    }

    public sealed class InvoiceAdjustmentInfo
    {
        public int HoaDonId { get; init; }
        public string MaHoaDon { get; init; } = string.Empty;
        public bool CoTheDieuChinh { get; init; }
        public string? LyDoKhoa { get; init; }
        public decimal ChiSoDienCu { get; init; }
        public decimal ChiSoDienMoi { get; init; }
        public decimal ChiSoNuocCu { get; init; }
        public decimal ChiSoNuocMoi { get; init; }
        public DateTime? HanPhanHoiUtc { get; init; }
    }

    public sealed class InvoiceAdjustmentResult
    {
        public int HoaDonIdCu { get; init; }
        public int HoaDonIdMoi { get; init; }
        public string MaHoaDonMoi { get; init; } = string.Empty;
        public decimal TongTienMoi { get; init; }
        public DateTime HanPhanHoiUtc { get; init; }
        public bool DaTaoHoaDonThayThe { get; init; }
    }
}
