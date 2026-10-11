namespace QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.DTOs
{
    public sealed class MeterAdjustmentRequest
    {
        public int DichVuDienNuocCuaPhongId { get; init; }
        public decimal ChiSoDienMoi { get; init; }
        public decimal ChiSoNuocMoi { get; init; }
        public string LyDo { get; init; } = string.Empty;
    }

    public sealed class MeterAdjustmentResult
    {
        public decimal ChiSoDienCu { get; init; }
        public decimal ChiSoDienMoi { get; init; }
        public decimal ChiSoNuocCu { get; init; }
        public decimal ChiSoNuocMoi { get; init; }
    }
}
