using System.Collections.Generic;

namespace QuanLyChoThuePhongTroWeb.Application.Features.BaoCaos.DTOs
{
    public sealed class BaoCaoDienNuocDto
    {
        public BaoCaoPhamViDto PhamVi { get; init; } = new();
        public decimal TongKwh { get; init; }
        public decimal TongM3 { get; init; }
        public decimal TienDien { get; init; }
        public decimal TienNuoc { get; init; }
        public decimal? TrungBinhKwhPhongThang { get; init; }
        public int SoBanGhi { get; init; }
        public int SoBanGhiBoQua { get; init; }
        public IReadOnlyList<BaoCaoDienNuocDongDto> TheoKy { get; init; } = new List<BaoCaoDienNuocDongDto>();
        public IReadOnlyList<BaoCaoPhongDienNuocDto> TopPhong { get; init; } = new List<BaoCaoPhongDienNuocDto>();
    }

    public sealed class BaoCaoDienNuocDongDto
    {
        public string Nhan { get; init; } = "";
        public int Thang { get; init; }
        public int Nam { get; init; }
        public decimal Kwh { get; init; }
        public decimal M3 { get; init; }
        public decimal TienDien { get; init; }
        public decimal TienNuoc { get; init; }
        public int SoBanGhi { get; init; }
        public int SoBanGhiBoQua { get; init; }
    }

    public sealed class BaoCaoPhongDienNuocDto
    {
        public int PhongTroId { get; init; }
        public string SoPhong { get; init; } = "";
        public string TenChiNhanh { get; init; } = "";
        public decimal Kwh { get; init; }
        public decimal M3 { get; init; }
        public decimal TienDien { get; init; }
        public decimal TienNuoc { get; init; }
        public int SoKy { get; init; }
    }
}
