using System;

namespace QuanLyChoThuePhongTroWeb.Application.Features.ThanhVienHopDongs.DTOs
{
    public class ThanhVienHopDongRes
    {
        public int ChiTietThanhVienHopDongId { get; set; }
        public int NguoiThueId { get; set; }
        public string HoVaTen { get; set; } = string.Empty;
        public string SoDienThoai { get; set; } = string.Empty;
        public string CCCD { get; set; } = string.Empty;
        public DateTime NgayVao { get; set; }
        public DateTime? NgayChuyenDi { get; set; }
        public bool IsChuHopDong { get; set; } // Để biết ai là người đứng tên
    }
}
