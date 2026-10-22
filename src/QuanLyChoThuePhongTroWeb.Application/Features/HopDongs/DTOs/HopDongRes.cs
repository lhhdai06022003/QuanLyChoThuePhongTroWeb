using System;
using QuanLyChoThuePhongTroWeb.Application.Common.Enums;

namespace QuanLyChoThuePhongTroWeb.Application.Features.HopDongs.DTOs
{
    public class HopDongRes
    {
        public int HopDongId { get; set; }
        public string MaHopDong { get; set; } = string.Empty;
        public int PhongTroId { get; set; }
        public string SoPhong { get; set; } = string.Empty;
        public string TenChiNhanh { get; set; } = string.Empty;

        public int NguoiThueId { get; set; }
        public string TenNguoiThue { get; set; } = string.Empty;

        public DateTime ThoiDiemBatDau { get; set; }
        public DateTime? ThoiDiemKetThuc { get; set; }
        public decimal TienCocPhong { get; set; }
        public decimal TienThuePhong { get; set; }
        public AppTrangThaiHopDong TrangThaiHopDong { get; set; }
        public DateTime NgayTao { get; set; }
        public DateTime? NgayCapNhat { get; set; }
    }
}
