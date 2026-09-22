using System;

namespace QuanLyChoThuePhongTroWeb.Application.Features.Emails.DTOs
{
    public class ContractExpiryAlertData
    {
        public string MaHopDong { get; set; } = string.Empty;
        public string SoPhong { get; set; } = string.Empty;
        public string TenChiNhanh { get; set; } = string.Empty;
        public string DiaChiChiNhanh { get; set; } = string.Empty;
        public string SoDienThoaiChiNhanh { get; set; } = string.Empty;
        public string TenNguoiThue { get; set; } = string.Empty;
        public DateTime ThoiDiemKetThuc { get; set; }
        public int SoNgayConLai { get; set; }
        public decimal TienThuePhong { get; set; }
    }
}
