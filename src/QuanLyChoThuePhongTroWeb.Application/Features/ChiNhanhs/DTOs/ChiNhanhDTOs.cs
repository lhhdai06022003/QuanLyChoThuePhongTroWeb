using System;

namespace QuanLyChoThuePhongTroWeb.Application.Features.ChiNhanhs.DTOs
{
    public class ChiNhanhRes
    {
        public int ChiNhanhId { get; set; }
        public string MaChiNhanh { get; set; } = string.Empty;
        public string TenChiNhanh { get; set; } = string.Empty;
        public string DiaChi { get; set; } = string.Empty;
        public string MoTa { get; set; } = string.Empty;
        public string SoDienThoai { get; set; } = string.Empty;
        public DateTime NgayTao { get; set; }
    }

    public class ChiNhanhReq
    {
        public int ChiNhanhId { get; set; }
        public string MaChiNhanh { get; set; } = string.Empty;
        public string TenChiNhanh { get; set; } = string.Empty;
        public string DiaChi { get; set; } = string.Empty;
        public string MoTa { get; set; } = string.Empty;
        public string SoDienThoai { get; set; } = string.Empty;
    }
}
