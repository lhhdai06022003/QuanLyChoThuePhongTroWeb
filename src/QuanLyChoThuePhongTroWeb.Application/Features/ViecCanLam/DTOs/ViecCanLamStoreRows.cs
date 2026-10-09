using System;

namespace QuanLyChoThuePhongTroWeb.Application.Features.ViecCanLam.DTOs
{
    // SoLuongGap chỉ dùng cho KH1, KH3; các việc khác để 0.
    public sealed class DemChiNhanhRow
    {
        public int ChiNhanhId { get; init; }
        public string TenChiNhanh { get; init; } = string.Empty;
        public int SoLuong { get; init; }
        public int SoLuongGap { get; init; }
    }

    public sealed class DemTheoKyRow
    {
        public int ChiNhanhId { get; init; }
        public string TenChiNhanh { get; init; } = string.Empty;
        public int Thang { get; init; }
        public int Nam { get; init; }
        public int SoLuong { get; init; }
    }

    // TongConNo chỉ dùng cho TT2, TT3; HD1-HD3 để 0.
    public sealed class DemHoaDonRow
    {
        public int ChiNhanhId { get; init; }
        public string TenChiNhanh { get; init; } = string.Empty;
        public int SoLuong { get; init; }
        public int NamCuNhat { get; init; }
        public decimal TongConNo { get; init; }
    }

    public sealed class HopDongChoChotChiSoRow
    {
        public int PhongTroId { get; init; }
        public DateTime ThoiDiemBatDau { get; init; }
        public int ChiNhanhId { get; init; }
        public string TenChiNhanh { get; init; } = string.Empty;
    }

    public sealed class KyChiSoDaChotRow
    {
        public int PhongTroId { get; init; }
        public int Thang { get; init; }
        public int Nam { get; init; }
    }

    public sealed class HoaDonQuaHanRow
    {
        public int HoaDonId { get; init; }
        public string MaHoaDon { get; init; } = string.Empty;
        public string SoPhong { get; init; } = string.Empty;
        public int ChiNhanhId { get; init; }
        public string TenChiNhanh { get; init; } = string.Empty;
        public string KhachThue { get; init; } = string.Empty;
        public decimal TongTien { get; init; }
        public decimal DaThu { get; init; }
        public DateTime HanThanhToan { get; init; }
    }
}
