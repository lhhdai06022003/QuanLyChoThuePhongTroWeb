namespace QuanLyChoThuePhongTroWeb.Application.Features.PhongTros.DTOs
{
    public sealed record PhongTroListItemDto
    {
        public int PhongTroId { get; init; }
        public string? TenChiNhanh { get; init; }
        public string SoPhong { get; init; } = string.Empty;
        public int TangLau { get; init; }
        public decimal GiaThue { get; init; }
        public double DienTich { get; init; }
        public int TrangThai { get; init; }
        public bool DuocDangTin { get; init; }
        public bool ChiNhanhConHoatDong { get; init; }
        public int SoAnhDangHoatDong { get; init; }
        public string? AnhDaiDienUrl { get; init; }
    }
}
