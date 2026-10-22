using System;
using System.Collections.Generic;

namespace QuanLyChoThuePhongTroWeb.Application.Features.HopDongs.DTOs
{
    /// <summary>
    /// DTO chi tiết hợp đồng – Dùng cho API GetById.
    /// Chỉ chứa các trường cần thiết, lọc bỏ thông tin nhạy cảm của NguoiThue/NguoiDung.
    /// </summary>
    public class HopDongDetailRes
    {
        public int HopDongId { get; set; }
        public string MaHopDong { get; set; } = string.Empty;

        // --- Thông tin Phòng (Đã flatten, không trả nested Entity) ---
        public int PhongTroId { get; set; }
        public string SoPhong { get; set; } = string.Empty;
        public int ChiNhanhId { get; set; }
        public string TenChiNhanh { get; set; } = string.Empty;

        // --- Thông tin Người đại diện (Chỉ lấy trường cần thiết) ---
        public int NguoiThueId { get; set; }
        public string TenNguoiThue { get; set; } = string.Empty;

        // --- Thời hạn & Tài chính ---
        public DateTime ThoiDiemBatDau { get; set; }
        public DateTime? ThoiDiemKetThuc { get; set; }
        public decimal TienCocPhong { get; set; }
        public decimal TienThuePhong { get; set; }

        // --- Trạng thái ---
        public int TrangThaiHopDong { get; set; }

        public List<string> DanhSachTieuDeDieuKhoan { get; set; } = new List<string>();
    }
}
