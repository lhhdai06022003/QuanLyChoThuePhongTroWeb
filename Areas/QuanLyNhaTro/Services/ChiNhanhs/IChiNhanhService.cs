using QuanLyChoThuePhongTroWeb.Models;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Services.ChiNhanhs
{
    public interface IChiNhanhService
    {
        Task<IEnumerable<ChiNhanh>> DanhSachChiNhanh();
        Task<ChiNhanh> GetChiNhanh(int id);
        Task<(bool IsSuccess, string Message)> ThemChiNhanhMoi(ChiNhanh chiNhanh);
        Task<(bool IsSuccess, string Message)> XoaChiNhanh(int id);
    }
}
