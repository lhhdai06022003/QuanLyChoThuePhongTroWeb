using System;
using System.Collections.Generic;

namespace QuanLyChoThuePhongTroWeb.Application.Features.ViecCanLam.DTOs
{
    public enum NhomViec
    {
        ChiSo = 0,
        HoaDon = 1,
        ThuTien = 2,
        KhachThueHopDong = 3
    }

    // Giá trị nhỏ = gấp hơn.
    public enum MucUuTienViec
    {
        Khan = 0,
        CanLam = 1,
        TheoDoi = 2
    }

    public sealed class ViecCanLamDto
    {
        public string MaViec { get; init; } = string.Empty;
        public NhomViec Nhom { get; init; }
        public MucUuTienViec MucUuTien { get; init; }
        public string TieuDe { get; init; } = string.Empty;
        public string MoTa { get; init; } = string.Empty;
        public int SoLuong { get; init; }
        public string Link { get; init; } = string.Empty;
        public int? Thang { get; init; }
        public int? Nam { get; init; }
        public decimal? TongConNo { get; init; }
    }

    public sealed class ViecCanLamTongHopDto
    {
        public int TongSo { get; init; }
        public int SoKhan { get; init; }
        public int SoCanLam { get; init; }
        public int SoTheoDoi { get; init; }
        public IReadOnlyList<ViecCanLamDto> Items { get; init; } = Array.Empty<ViecCanLamDto>();
        public static ViecCanLamTongHopDto Rong { get; } = new();
    }

    public sealed class HoaDonQuaHanDto
    {
        public int HoaDonId { get; init; }
        public string MaHoaDon { get; init; } = string.Empty;
        public string SoPhong { get; init; } = string.Empty;
        public string TenChiNhanh { get; init; } = string.Empty;
        public string KhachThue { get; init; } = string.Empty;
        public decimal SoConNo { get; init; }
        public DateTime HanThanhToanUtc { get; init; }
        public int SoNgayQuaHan { get; init; }
    }

    public sealed class DanhSachHoaDonQuaHanDto
    {
        public IReadOnlyList<HoaDonQuaHanDto> Items { get; init; } = Array.Empty<HoaDonQuaHanDto>();
        public int TongSo { get; init; }
    }
}
