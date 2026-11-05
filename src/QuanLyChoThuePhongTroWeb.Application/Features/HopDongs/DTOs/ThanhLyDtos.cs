using System;
using System.Collections.Generic;

namespace QuanLyChoThuePhongTroWeb.Application.Features.HopDongs.DTOs
{
    public sealed class ThanhLyHoaDonDto
    {
        public int HoaDonId { get; set; }
        public string MaHoaDon { get; set; } = string.Empty;
        public int HopDongId { get; set; }
        public string MaHopDong { get; set; } = string.Empty;
        public int Thang { get; set; }
        public int Nam { get; set; }
        public decimal TongTien { get; set; }
        public decimal DaThu { get; set; }
        public decimal ConLai { get; set; }
        public decimal CanTru { get; set; }
    }

    public sealed class ThanhLyHopDongPreviewDto
    {
        public int HopDongId { get; set; }
        public string MaHopDong { get; set; } = string.Empty;
        public string SoPhong { get; set; } = string.Empty;
        public string TenChiNhanh { get; set; } = string.Empty;
        public string TenNguoiDaiDien { get; set; } = string.Empty;
        public List<string> ThanhVienDangO { get; set; } = new();
        public DateTime ThoiDiemBatDauUtc { get; set; }
        public DateTime? ThoiDiemKetThucUtc { get; set; }
        public decimal TienCoc { get; set; }
        // Hóa đơn nợ của cả chuỗi gia hạn (cùng mã gốc).
        public List<ThanhLyHoaDonDto> HoaDonNo { get; set; } = new();
        public decimal TongNo { get; set; }
        public decimal CanTru { get; set; }
        public decimal CocHoanTrucTiep { get; set; }
        public decimal ConNoSauCanTru { get; set; }
        public List<string> LyDoChan { get; set; } = new();
        public bool CoTheThanhLy => LyDoChan.Count == 0;
    }

    public sealed class ThanhLyHopDongRequest
    {
        public int HopDongId { get; set; }
        public decimal TongNoLucMo { get; set; }
        public bool XacNhanConNo { get; set; }
    }

    public sealed class ThanhLyHopDongResult
    {
        public int HopDongId { get; set; }
        public decimal CanTru { get; set; }
        public decimal CocHoanTrucTiep { get; set; }
        public decimal ConNoSauCanTru { get; set; }
    }

    public sealed class BienBanHoaDonDto
    {
        public string MaHoaDon { get; set; } = string.Empty;
        public string MaHopDong { get; set; } = string.Empty;
        public int Thang { get; set; }
        public int Nam { get; set; }
        public decimal TongTien { get; set; }
        public decimal DaThuKhac { get; set; }
        public decimal CanTru { get; set; }
        public decimal ConLai { get; set; }
    }

    public sealed class BienBanThanhLyDto
    {
        public string MaHopDong { get; set; } = string.Empty;
        public string TenChiNhanh { get; set; } = string.Empty;
        public string DiaChiChiNhanh { get; set; } = string.Empty;
        public string SoDienThoaiChiNhanh { get; set; } = string.Empty;
        public string SoPhong { get; set; } = string.Empty;
        public string TenNguoiDaiDien { get; set; } = string.Empty;
        public string CccdNguoiDaiDien { get; set; } = string.Empty;
        public string SdtNguoiDaiDien { get; set; } = string.Empty;
        public List<string> ThanhVien { get; set; } = new();
        public DateTime ThoiDiemBatDauUtc { get; set; }
        public DateTime ThoiDiemKetThucUtc { get; set; }
        public DateTime NgayThanhLyUtc { get; set; }
        public decimal TienCoc { get; set; }
        public decimal TongCanTru { get; set; }
        public decimal CocHoanTrucTiep { get; set; }
        public decimal NoConLai { get; set; }
        public DateTime NgayInUtc { get; set; }
        public List<BienBanHoaDonDto> HoaDons { get; set; } = new();
        public string? GhiChu { get; set; }
    }
}
