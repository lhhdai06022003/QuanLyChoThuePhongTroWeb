using System;

namespace QuanLyChoThuePhongTroWeb.Application.Features.LichSuThanhToans.DTOs
{
    public class LichSuThanhToanGiaoDichRes
    {
        public int LichSuThanhToanId { get; set; }
        public string MaGiaoDich { get; set; } = string.Empty;
        public int HoaDonId { get; set; }
        public string MaHoaDon { get; set; } = string.Empty;
        public string TenPhong { get; set; } = string.Empty;
        public string TenNguoiThue { get; set; } = string.Empty;
        public string TenChiNhanh { get; set; } = string.Empty;
        public double SoTienThanhToan { get; set; }
        public int PhuongThucThanhToan { get; set; } // 0: Tiền mặt, 1: Chuyển khoản
        public string PhuongThucThanhToanText { get; set; } = string.Empty;
        public string NgayThanhToan { get; set; } = string.Empty;
        public string NguoiXacNhan { get; set; } = string.Empty;
        public string GhiChu { get; set; } = string.Empty;
    }

    public class ThongKeThanhToanRes
    {
        public double TongDoanhThu { get; set; }
        public int TongSoGiaoDich { get; set; }
        public double DoanhThuTienMat { get; set; }
        public double DoanhThuChuyenKhoan { get; set; }
    }
}
