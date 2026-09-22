using System.Collections.Generic;

namespace QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.DTOs
{
    // Request để phát sinh hóa đơn hàng loạt
    public class PhatSinhHoaDonReq
    {
        public int ChiNhanhId { get; set; }
        public int Thang { get; set; }
        public int Nam { get; set; }
        public List<int> SelectedPhongTroIds { get; set; } = new();
    }

    // Request để thu tiền
    public class ThuTienReq
    {
        public int HoaDonId { get; set; }
        public int PhuongThucThanhToan { get; set; } // 0 = Tiền mặt, 1 = Chuyển khoản
        public string GhiChu { get; set; } = string.Empty;
    }

    // Response hiển thị danh sách hóa đơn
    public class HoaDonRes
    {
        public int HoaDonId { get; set; }
        public string MaHoaDon { get; set; } = string.Empty;
        public int HopDongId { get; set; }
        public string MaHopDong { get; set; } = string.Empty;
        public string TenPhong { get; set; } = string.Empty;
        public string TenNguoiThue { get; set; } = string.Empty;
        public string TenChiNhanh { get; set; } = string.Empty;
        public int Thang { get; set; }
        public int Nam { get; set; }
        public decimal TongTien { get; set; }
        public string TrangThaiHoaDon { get; set; } = string.Empty;
        public int TrangThaiHoaDonValue { get; set; }
        public string NgayTao { get; set; } = string.Empty;
        public string SoDienThoai { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }

    // Response chi tiết hóa đơn (bao gồm các dòng dịch vụ)
    public class HoaDonChiTietRes
    {
        public int HoaDonId { get; set; }
        public string MaHoaDon { get; set; } = string.Empty;
        public string TenPhong { get; set; } = string.Empty;
        public string TenNguoiThue { get; set; } = string.Empty;
        public string TenChiNhanh { get; set; } = string.Empty;
        public string DiaChiChiNhanh { get; set; } = string.Empty;
        public string SoDienThoaiChiNhanh { get; set; } = string.Empty;
        public int Thang { get; set; }
        public int Nam { get; set; }
        public decimal TongTien { get; set; }
        public string TrangThaiHoaDon { get; set; } = string.Empty;
        public string NgayTao { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public List<ChiTietHoaDonRes> ChiTietHoaDons { get; set; } = new();
        public List<LichSuThanhToanRes> LichSuThanhToans { get; set; } = new();
    }

    // Response cho từng dòng chi tiết
    public class ChiTietHoaDonRes
    {
        public int ChiTietHoaDonId { get; set; }
        public string TenDichVu { get; set; } = string.Empty;
        public decimal DonGia { get; set; }
        public decimal SoLuong { get; set; }
        public decimal TongTien { get; set; }
        public string DonVi { get; set; } = string.Empty;
    }

    // Response cho lịch sử thanh toán
    public class LichSuThanhToanRes
    {
        public int LichSuThanhToanId { get; set; }
        public string MaGiaoDich { get; set; } = string.Empty;
        public decimal SoTienThanhToan { get; set; }
        public string PhuongThucThanhToan { get; set; } = string.Empty;
        public string NgayThanhToan { get; set; } = string.Empty;
        public string NguoiXacNhan { get; set; } = string.Empty;
        public string GhiChu { get; set; } = string.Empty;
    }

    // Response dùng khi xem trước phát sinh hóa đơn
    public class PhatSinhPreviewRes
    {
        public int PhongTroId { get; set; }
        public int HopDongId { get; set; }
        public string SoPhong { get; set; } = string.Empty;
        public string TenNguoiThue { get; set; } = string.Empty;
        public decimal TienPhongDuKien { get; set; }
        public int SoNgayO { get; set; }
        public int TongSoNgayTrongThang { get; set; }
        public bool DaChotDienNuoc { get; set; }
        public bool DaCoHoaDon { get; set; }
        public bool HopDongHopLe { get; set; }
        public string GhiChuTrangThai { get; set; } = string.Empty;
        public decimal TongTienDuKien { get; set; }
        public int SuCoCount { get; set; }
    }

    public class PhatSinhHoaDonResult
    {
        public bool IsSuccess { get; set; }
        public string Message { get; set; } = string.Empty;
        public int SoHoaDonMoi { get; set; }
        public List<string> Successes { get; set; } = new();
        public List<string> Skipped { get; set; } = new();
    }

    // Request chỉnh sửa hóa đơn
    public class UpdateHoaDonReq
    {
        public List<ChiTietHoaDonUpdateReq> ChiTiets { get; set; } = new();
    }

    public class ChiTietHoaDonUpdateReq
    {
        public int? ChiTietHoaDonId { get; set; }
        public string TenDichVu { get; set; } = string.Empty;
        public decimal DonGia { get; set; }
        public decimal SoLuong { get; set; }
        public int? DichVuId { get; set; }
    }
}
