using System;
using System.Collections.Generic;

namespace QuanLyChoThuePhongTroWeb.Application.Features.HopDongs.DTOs
{
    public sealed class GiaHanHopDongFormDto
    {
        public int HopDongId { get; set; }
        public string MaHopDong { get; set; } = string.Empty;
        public string MaHopDongMoiDuKien { get; set; } = string.Empty;
        public string SoPhong { get; set; } = string.Empty;
        public string TenChiNhanh { get; set; } = string.Empty;
        public string TenNguoiDaiDien { get; set; } = string.Empty;
        public DateTime ThoiDiemBatDauUtc { get; set; }
        public DateTime? ThoiDiemKetThucUtc { get; set; }
        public decimal TienThue { get; set; }
        public decimal TienCoc { get; set; }
        public int ThangBatDauMacDinh { get; set; }
        public int NamBatDauMacDinh { get; set; }
        public DateOnly NgayKetThucMacDinh { get; set; }
        public List<string> DieuKhoanHienTai { get; set; } = new();
        public List<string> ThanhVienDangO { get; set; } = new();
    }

    public sealed class GiaHanHopDongRequest
    {
        public int HopDongId { get; set; }
        public int ThangBatDau { get; set; }
        public int NamBatDau { get; set; }
        public DateOnly NgayKetThucMoi { get; set; }
        public decimal TienThue { get; set; }
        public decimal TienCoc { get; set; }
        // null: chép điều khoản của hợp đồng cũ; rỗng: không có điều khoản; có giá trị: dùng các điều khoản mẫu đó.
        public List<int>? DieuKhoanMauIds { get; set; }
    }

    public sealed class GiaHanHopDongResult
    {
        public int HopDongMoiId { get; set; }
        public string MaHopDongMoi { get; set; } = string.Empty;
    }
}
