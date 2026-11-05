using System;
using System.Collections.Generic;

namespace QuanLyChoThuePhongTroWeb.Application.Features.HopDongs.DTOs
{
    public sealed class ThanhVienDto
    {
        public string HoVaTen { get; set; } = string.Empty;
        public DateTime NgayVaoUtc { get; set; }
        public DateTime? NgayChuyenDiUtc { get; set; }
    }

    public sealed class ChuoiHopDongItemDto
    {
        public int HopDongId { get; set; }
        public string MaHopDong { get; set; } = string.Empty;
        public string LoaiText { get; set; } = string.Empty;
        public DateTime TuUtc { get; set; }
        public DateTime? DenUtc { get; set; }
        public string TrangThaiText { get; set; } = string.Empty;
        public bool LaHienTai { get; set; }
    }

    public sealed class HopDongChiTietDto
    {
        public int HopDongId { get; set; }
        public string MaHopDong { get; set; } = string.Empty;
        public string MaGoc { get; set; } = string.Empty;
        public string LoaiText { get; set; } = string.Empty;
        public int PhongTroId { get; set; }
        public string SoPhong { get; set; } = string.Empty;
        public int ChiNhanhId { get; set; }
        public string TenChiNhanh { get; set; } = string.Empty;
        public int NguoiThueId { get; set; }
        public string TenNguoiDaiDien { get; set; } = string.Empty;
        public DateTime ThoiDiemBatDauUtc { get; set; }
        public DateTime? ThoiDiemKetThucUtc { get; set; }
        public decimal TienThue { get; set; }
        public decimal TienCoc { get; set; }
        public int TrangThai { get; set; }
        public string TrangThaiText { get; set; } = string.Empty;
        public int? HopDongKeTiepId { get; set; }
        public string? MaHopDongKeTiep { get; set; }
        public bool CoTheThanhLy { get; set; }
        public bool CoTheGiaHan { get; set; }
        public bool CoBienBanThanhLy { get; set; }
        public List<ThanhVienDto> ThanhVien { get; set; } = new();
        public List<string> DieuKhoan { get; set; } = new();
        public List<ChuoiHopDongItemDto> ChuoiHopDong { get; set; } = new();
    }

    public sealed class LichSuThueDongDto
    {
        public int HopDongId { get; set; }
        public string MaHopDong { get; set; } = string.Empty;
        // "Đứng tên" | "Ở ghép"
        public string VaiTro { get; set; } = string.Empty;
        public string LoaiText { get; set; } = string.Empty;
        public string SoPhong { get; set; } = string.Empty;
        public string TenChiNhanh { get; set; } = string.Empty;
        public DateTime TuUtc { get; set; }
        public DateTime? DenUtc { get; set; }
        public string TrangThaiText { get; set; } = string.Empty;
        public decimal ConNo { get; set; }
    }

    public sealed class LichSuThueDto
    {
        public int NguoiThueId { get; set; }
        public string HoVaTen { get; set; } = string.Empty;
        public string CCCD { get; set; } = string.Empty;
        public string SoDienThoai { get; set; } = string.Empty;
        public int SoLanThue { get; set; }
        public int SoHopDong { get; set; }
        public int SoLanOGhep { get; set; }
        public int TongSoThang { get; set; }
        public decimal NoHienTai { get; set; }
        public List<LichSuThueDongDto> Dong { get; set; } = new();
    }

    public sealed class NoCuHoaDonDto
    {
        public string MaHoaDon { get; set; } = string.Empty;
        public string MaHopDong { get; set; } = string.Empty;
        public int Thang { get; set; }
        public int Nam { get; set; }
        public decimal ConLai { get; set; }
    }

    public sealed class NoCuDto
    {
        public decimal TongNo { get; set; }
        public List<NoCuHoaDonDto> HoaDons { get; set; } = new();
    }
}
