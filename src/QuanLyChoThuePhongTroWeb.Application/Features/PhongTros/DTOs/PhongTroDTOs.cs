using System;
using QuanLyChoThuePhongTroWeb.Application.Common.Enums;

namespace QuanLyChoThuePhongTroWeb.Application.Features.PhongTros.DTOs
{
    public class PhongTroReq
    {
        public int PhongTroId { get; set; }
        public int ChiNhanhId { get; set; }
        public string SoPhong { get; set; } = string.Empty;
        public int TangLau { get; set; }
        public decimal GiaThue { get; set; }
        public double DienTich { get; set; }
        public int SoNguoiToiDa { get; set; }
        public AppTrangThaiPhong TrangThai { get; set; }
        public string? MoTa { get; set; }
    }

    public class PhongTroRes
    {
        public int PhongTroId { get; set; }
        public int ChiNhanhId { get; set; }
        public string? TenChiNhanh { get; set; }
        public string SoPhong { get; set; } = string.Empty;
        public int TangLau { get; set; }
        public decimal GiaThue { get; set; }
        public double DienTich { get; set; }
        public int SoNguoiToiDa { get; set; }
        public AppTrangThaiPhong TrangThai { get; set; }
        public string? MoTa { get; set; }
        public DateTime NgayTao { get; set; }
    }

    public class PhongTroOptionDto
    {
        public int PhongTroId { get; set; }
        public int ChiNhanhId { get; set; }
        public string SoPhong { get; set; } = string.Empty;
        public decimal GiaThue { get; set; }
        public AppTrangThaiPhong TrangThai { get; set; }
    }
}
