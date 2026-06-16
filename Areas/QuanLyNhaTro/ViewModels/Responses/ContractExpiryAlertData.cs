using System;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels.Responses
{
    public class ContractExpiryAlertData
    {
        public string MaHopDong { get; set; }
        public string SoPhong { get; set; }
        public string TenChiNhanh { get; set; }
        public string DiaChiChiNhanh { get; set; }
        public string SoDienThoaiChiNhanh { get; set; }
        public string TenNguoiThue { get; set; }
        public DateTime ThoiDiemKetThuc { get; set; }
        public int SoNgayConLai { get; set; }
        public double TienThuePhong { get; set; }
    }
}
