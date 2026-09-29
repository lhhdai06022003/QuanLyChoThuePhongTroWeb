using System;

namespace QuanLyChoThuePhongTroWeb.Application.Features.LichSuThanhToans.DTOs
{
    public sealed record LichSuThanhToanKhachThueDto
    {
        public int LichSuThanhToanId { get; init; }
        public string? MaGiaoDich { get; init; }
        public string MaHoaDon { get; init; } = string.Empty;
        public string SoPhong { get; init; } = string.Empty;
        public double SoTienThanhToan { get; init; }
        public int PhuongThucThanhToan { get; init; }
        public DateTime NgayThanhToan { get; init; }
        public string? GhiChu { get; init; }
    }
}
