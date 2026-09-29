using System;

namespace QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Services
{
    public interface IHoaDonCalculatorService
    {
        (double SoTien, string DienGiai, int SoNgayO) TinhTienPhong(double giaThue, DateTime batDau, DateTime? ketThuc, int thang, int nam);
        
        (double SoLuong, double SoTien, string DienGiai) TinhTienDienNuoc(double chiSoMoi, double chiSoCu, double donGia, string tenDichVu, int soNgayO, int tongNgayTrongThang);
        
        (double SoTien, string DienGiai) TinhTienDichVuCoDinh(double giaDv, int soLuong, string tenDichVu, DateTime batDau, DateTime? ketThuc, int thang, int nam, DateTime? contractStart = null, DateTime? contractEnd = null);
    }
}
