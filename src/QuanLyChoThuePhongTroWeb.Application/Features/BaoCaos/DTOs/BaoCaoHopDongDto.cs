using System.Collections.Generic;

namespace QuanLyChoThuePhongTroWeb.Application.Features.BaoCaos.DTOs
{
    public sealed class BaoCaoHopDongDto
    {
        public BaoCaoPhamViDto PhamVi { get; init; } = new();
        public int DangHoatDong { get; init; }
        public int QuaHanChuaXuLy { get; init; }
        public int SapHetHan { get; init; }
        public BaoCaoHopDongDongDto Tong { get; init; } = new();
        public IReadOnlyList<BaoCaoHopDongDongDto> TheoKy { get; init; } = new List<BaoCaoHopDongDongDto>();
    }

    public sealed class BaoCaoHopDongDongDto
    {
        public string Nhan { get; init; } = "";
        public int? Thang { get; init; }
        public int? Nam { get; init; }
        public int HopDongMoi { get; init; }
        public int GiaHan { get; init; }
        public int TraPhong { get; init; }
        public int HetHanTrongKy { get; init; }
        public int HetHanDaKetThuc { get; init; }
        public int HetHanCoGiaHan { get; init; }
        public decimal? TyLeGiaHan { get; init; }
    }
}
