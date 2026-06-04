using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels.Requests;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Services.DienNuocs
{
    public interface IDienNuocService
    {
        Task<List<DienNuocPhongRes>> GetDanhSachDienNuocAsync(int chiNhanhId, int thang, int nam);
        Task<(bool IsSuccess, string ErrorMessage)> SaveChotDienNuocAsync(ChotDienNuocReq input);
    }

    public class DienNuocPhongRes
    {
        public int DichVuDienNuocCuaPhongId { get; set; }
        public int PhongTroId { get; set; }
        public string TenPhong { get; set; }
        public string TenNguoiDaiDien { get; set; }
        
        public double ChiSoDienCu { get; set; }
        public double ChiSoDienMoi { get; set; }
        
        public double ChiSoNuocCu { get; set; }
        public double ChiSoNuocMoi { get; set; }

        public bool IsDaChot { get; set; } // true if this record already exists in DB for the given month/year
        public bool IsLocked { get; set; }
    }
}
