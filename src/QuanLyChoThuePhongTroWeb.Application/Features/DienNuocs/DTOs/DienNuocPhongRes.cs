namespace QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.DTOs
{
    public class DienNuocPhongRes
    {
        public int DichVuDienNuocCuaPhongId { get; set; }
        public int PhongTroId { get; set; }
        public string TenPhong { get; set; } = string.Empty;
        public string TenNguoiDaiDien { get; set; } = string.Empty;
        
        public double ChiSoDienCu { get; set; }
        public double ChiSoDienMoi { get; set; }
        
        public double ChiSoNuocCu { get; set; }
        public double ChiSoNuocMoi { get; set; }

        public bool IsDaChot { get; set; } // true if this record already exists in DB for the given month/year
        public bool IsLocked { get; set; }
    }
}
