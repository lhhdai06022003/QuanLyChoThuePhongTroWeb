namespace QuanLyChoThuePhongTroWeb.Application.Features.BaoCaos.DTOs
{
    public sealed class BaoCaoFilter
    {
        public int? ChiNhanhId { get; init; }
        public int TuThang { get; init; }
        public int TuNam { get; init; }
        public int DenThang { get; init; }
        public int DenNam { get; init; }
    }
}
