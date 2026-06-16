using System.Collections.Generic;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels
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
        public string GhiChu { get; set; }
    }

    // Response hiển thị danh sách hóa đơn
    public class HoaDonRes
    {
        public int HoaDonId { get; set; }
        public string MaHoaDon { get; set; }
        public int HopDongId { get; set; }
        public string MaHopDong { get; set; }
        public string TenPhong { get; set; }
        public string TenNguoiThue { get; set; }
        public string TenChiNhanh { get; set; }
        public int Thang { get; set; }
        public int Nam { get; set; }
        public double TongTien { get; set; }
        public string TrangThaiHoaDon { get; set; }
        public int TrangThaiHoaDonValue { get; set; }
        public string NgayTao { get; set; }
        public string SoDienThoai { get; set; }
        public string Email { get; set; }
    }

    // Response chi tiết hóa đơn (bao gồm các dòng dịch vụ)
    public class HoaDonChiTietRes
    {
        public int HoaDonId { get; set; }
        public string MaHoaDon { get; set; }
        public string TenPhong { get; set; }
        public string TenNguoiThue { get; set; }
        public string TenChiNhanh { get; set; }
        public string DiaChiChiNhanh { get; set; }
        public string SoDienThoaiChiNhanh { get; set; }
        public int Thang { get; set; }
        public int Nam { get; set; }
        public double TongTien { get; set; }
        public string TrangThaiHoaDon { get; set; }
        public string NgayTao { get; set; }
        public string Email { get; set; }
        public List<ChiTietHoaDonRes> ChiTietHoaDons { get; set; } = new();
        public List<LichSuThanhToanRes> LichSuThanhToans { get; set; } = new();
    }

    // Response cho từng dòng chi tiết
    public class ChiTietHoaDonRes
    {
        public int ChiTietHoaDonId { get; set; }
        public string TenDichVu { get; set; }
        public double DonGia { get; set; }
        public int SoLuong { get; set; }
        public double TongTien { get; set; }
        public string DonVi { get; set; }
    }

    // Response cho lịch sử thanh toán
    public class LichSuThanhToanRes
    {
        public int LichSuThanhToanId { get; set; }
        public string MaGiaoDich { get; set; }
        public double SoTienThanhToan { get; set; }
        public string PhuongThucThanhToan { get; set; }
        public string NgayThanhToan { get; set; }
        public string NguoiXacNhan { get; set; }
        public string GhiChu { get; set; }
    }

    // Response dùng khi xem trước phát sinh hóa đơn
    public class PhatSinhPreviewRes
    {
        public int PhongTroId { get; set; }
        public string SoPhong { get; set; }
        public string TenNguoiThue { get; set; }
        public double TienPhongDuKien { get; set; }
        public int SoNgayO { get; set; }
        public int TongSoNgayTrongThang { get; set; }
        public bool DaChotDienNuoc { get; set; }
        public bool DaCoHoaDon { get; set; }
        public bool HopDongHopLe { get; set; }
        public string GhiChuTrangThai { get; set; }
        public double TongTienDuKien { get; set; }
    }

    // Request chỉnh sửa hóa đơn
    public class UpdateHoaDonReq
    {
        public List<ChiTietHoaDonUpdateReq> ChiTiets { get; set; } = new();
    }

    public class ChiTietHoaDonUpdateReq
    {
        public int? ChiTietHoaDonId { get; set; }
        public string TenDichVu { get; set; }
        public double DonGia { get; set; }
        public int SoLuong { get; set; }
        public int? DichVuId { get; set; }
    }
}
