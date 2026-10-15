namespace QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.DTOs
{
    public sealed class MeterAnomalyCheckRequest
    {
        public int PhongTroId { get; init; }
        public int Thang { get; init; }
        public int Nam { get; init; }
        public decimal? ChiSoDienMoi { get; init; }
        public decimal? ChiSoNuocMoi { get; init; }
    }

    // MaLyDo = tên MeterAnomalyReason.
    public sealed class MeterAnomalyWarning
    {
        public string MaLyDo { get; init; } = "";
        public string MoTa { get; init; } = "";
    }

    public sealed class MeterAnomalyCheckResult
    {
        public MeterAnomalyWarning? Dien { get; init; }
        public MeterAnomalyWarning? Nuoc { get; init; }
    }
}
