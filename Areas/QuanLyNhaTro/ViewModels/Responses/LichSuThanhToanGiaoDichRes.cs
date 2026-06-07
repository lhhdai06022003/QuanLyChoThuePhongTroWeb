using System;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels.Responses
{
    public class LichSuThanhToanGiaoDichRes
    {
        public int LichSuThanhToanId { get; set; }
        public string MaGiaoDich { get; set; }
        public int HoaDonId { get; set; }
        public string MaHoaDon { get; set; }
        public string TenPhong { get; set; }
        public string TenNguoiThue { get; set; }
        public string TenChiNhanh { get; set; }
        public double SoTienThanhToan { get; set; }
        public int PhuongThucThanhToan { get; set; } // 0: Tiền mặt, 1: Chuyển khoản
        public string PhuongThucThanhToanText { get; set; }
        public string NgayThanhToan { get; set; }
        public string NguoiXacNhan { get; set; }
        public string GhiChu { get; set; }
    }

    public class ThongKeThanhToanRes
    {
        public double TongDoanhThu { get; set; }
        public int TongSoGiaoDich { get; set; }
        public double DoanhThuTienMat { get; set; }
        public double DoanhThuChuyenKhoan { get; set; }
    }
}
