using System.Collections.Generic;

namespace QuanLyChoThuePhongTroWeb.Application.Features.BaoCaos.DTOs
{
    public sealed class BaoCaoCongSuatDto
    {
        public BaoCaoPhamViDto PhamVi { get; init; } = new();
        public int TongPhong { get; init; }
        public int DangThue { get; init; }
        public int Trong { get; init; }
        public int BaoTri { get; init; }
        public BaoCaoCongSuatDongDto Tong { get; init; } = new();
        public IReadOnlyList<BaoCaoCongSuatDongDto> TheoKy { get; init; } = new List<BaoCaoCongSuatDongDto>();
        public IReadOnlyList<BaoCaoCongSuatDongDto> TheoChiNhanh { get; init; } = new List<BaoCaoCongSuatDongDto>();
        public IReadOnlyList<BaoCaoPhongTrongDto> PhongTrongLauNhat { get; init; } = new List<BaoCaoPhongTrongDto>();
    }

    public sealed class BaoCaoCongSuatDongDto
    {
        public string Nhan { get; init; } = "";
        public int? Thang { get; init; }
        public int? Nam { get; init; }
        public int? ChiNhanhId { get; init; }
        public int NgayPhongCoTheThue { get; init; }
        public int NgayPhongDaThue { get; init; }
        public decimal? TyLeLapDay { get; init; }
    }

    public sealed class BaoCaoPhongTrongDto
    {
        public int PhongTroId { get; init; }
        public string SoPhong { get; init; } = "";
        public string TenChiNhanh { get; init; } = "";
        public int SoNgayTrong { get; init; }
        // Định dạng dd/MM/yyyy.
        public string TuNgayText { get; init; } = "";
    }
}
