using System.Collections.Generic;
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
        public decimal SoTienThanhToan { get; set; }
        public int PhuongThucThanhToan { get; set; } // 0: Tiền mặt, 1: Chuyển khoản
        public string PhuongThucThanhToanText { get; set; } = string.Empty;
        public string NgayThanhToan { get; set; } = string.Empty;
        public string NguoiXacNhan { get; set; } = string.Empty;
        public string GhiChu { get; set; } = string.Empty;
        // Tách từ các thẻ [CHENH-LECH], [TIEN-THUA] trong GhiChu để hiển thị (spec thanh toán §16.2, §27.2).
        public decimal? TienThua { get; set; }
        public decimal? ChenhLech { get; set; }
        public List<string> NhanGhiChu { get; set; } = new();
        public string NoiDungGhiChu { get; set; } = string.Empty;
    }

    public class ThongKeThanhToanRes
    {
        public decimal TongDoanhThu { get; set; }
        public int TongSoGiaoDich { get; set; }
        public decimal DoanhThuTienMat { get; set; }
        public decimal DoanhThuChuyenKhoan { get; set; }
    }
}
