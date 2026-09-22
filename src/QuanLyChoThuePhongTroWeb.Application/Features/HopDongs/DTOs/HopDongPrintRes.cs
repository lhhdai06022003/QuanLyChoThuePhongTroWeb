using System;
using System.Collections.Generic;

namespace QuanLyChoThuePhongTroWeb.Application.Features.HopDongs.DTOs
{
    public class HopDongPrintRes
    {
        public int HopDongId { get; set; }
        public string MaHopDong { get; set; } = string.Empty;
        public DateTime ThoiDiemBatDau { get; set; }
        public DateTime? ThoiDiemKetThuc { get; set; }
        public decimal TienCocPhong { get; set; }
        public decimal TienThuePhong { get; set; }
        public DateTime NgayTao { get; set; }

        // Thông tin Bên A (Chi nhánh/Quản lý)
        public string TenChiNhanh { get; set; } = string.Empty;
        public string DiaChiChiNhanh { get; set; } = string.Empty;
        public string SoDienThoaiChiNhanh { get; set; } = string.Empty;
        public string SoPhong { get; set; } = string.Empty;

        // Thông tin Bên B (Người đại diện)
        public string HoVaTenNguoiThue { get; set; } = string.Empty;
        public string SoDienThoaiNguoiThue { get; set; } = string.Empty;
        public string CCCDNguoiThue { get; set; } = string.Empty;
        public DateTime? NgayCapCCCD { get; set; }
        public string NoiCapCCCD { get; set; } = string.Empty;
        public string QueQuan { get; set; } = string.Empty;

        // Chi phí dịch vụ
        public List<HopDongDichVuPrintRes> DichVus { get; set; } = new List<HopDongDichVuPrintRes>();

        // Điều khoản bổ sung
        public List<HopDongDieuKhoanPrintRes> DieuKhoans { get; set; } = new List<HopDongDieuKhoanPrintRes>();
    }

    public class HopDongDichVuPrintRes
    {
        public string TenDichVu { get; set; } = string.Empty;
        public decimal DonGia { get; set; }
        public string DonViTinh { get; set; } = string.Empty;
    }

    public class HopDongDieuKhoanPrintRes
    {
        public string TieuDe { get; set; } = string.Empty;
        public string NoiDung { get; set; } = string.Empty;
        public int ThuTu { get; set; }
    }
}
