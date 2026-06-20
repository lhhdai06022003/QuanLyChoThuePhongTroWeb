namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels.Responses
{
    public class HopDongPrintRes
    {
        public int HopDongId { get; set; }
        public string MaHopDong { get; set; }
        public DateTime ThoiDiemBatDau { get; set; }
        public DateTime? ThoiDiemKetThuc { get; set; }
        public double TienCocPhong { get; set; }
        public double TienThuePhong { get; set; }
        public DateTime NgayTao { get; set; }

        // Thông tin Bên A (Chi nhánh/Quản lý)
        public string TenChiNhanh { get; set; }
        public string DiaChiChiNhanh { get; set; }
        public string SoDienThoaiChiNhanh { get; set; }
        public string SoPhong { get; set; }

        // Thông tin Bên B (Người đại diện)
        public string HoVaTenNguoiThue { get; set; }
        public string SoDienThoaiNguoiThue { get; set; }
        public string CCCDNguoiThue { get; set; }
        public DateTime? NgayCapCCCD { get; set; }
        public string NoiCapCCCD { get; set; }
        public string QueQuan { get; set; }

        // Chi phí dịch vụ
        public List<HopDongDichVuPrintRes> DichVus { get; set; } = new List<HopDongDichVuPrintRes>();

        // Điều khoản bổ sung
        public List<HopDongDieuKhoanPrintRes> DieuKhoans { get; set; } = new List<HopDongDieuKhoanPrintRes>();
    }

    public class HopDongDichVuPrintRes
    {
        public string TenDichVu { get; set; }
        public double DonGia { get; set; }
        public string DonViTinh { get; set; }
    }

    public class HopDongDieuKhoanPrintRes
    {
        public string TieuDe { get; set; }
        public string NoiDung { get; set; }
        public int ThuTu { get; set; }
    }
}
