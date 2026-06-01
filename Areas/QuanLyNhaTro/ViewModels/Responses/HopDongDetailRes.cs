namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels.Responses
{
    /// <summary>
    /// DTO chi tiết hợp đồng – Dùng cho API GetById.
    /// Chỉ chứa các trường cần thiết, lọc bỏ thông tin nhạy cảm của NguoiThue/NguoiDung.
    /// </summary>
    public class HopDongDetailRes
    {
        public int HopDongId { get; set; }
        public string MaHopDong { get; set; }

        // --- Thông tin Phòng (Đã flatten, không trả nested Entity) ---
        public int PhongTroId { get; set; }
        public string SoPhong { get; set; }
        public int ChiNhanhId { get; set; }
        public string TenChiNhanh { get; set; }

        // --- Thông tin Người đại diện (Chỉ lấy trường cần thiết) ---
        public int NguoiThueId { get; set; }
        public string TenNguoiThue { get; set; }

        // --- Thời hạn & Tài chính ---
        public DateTime ThoiDiemBatDau { get; set; }
        public DateTime? ThoiDiemKetThuc { get; set; }
        public double TienCocPhong { get; set; }
        public double TienThuePhong { get; set; }

        // --- Trạng thái ---
        public int TrangThaiHopDong { get; set; }
    }
}
