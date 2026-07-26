using QuanLyChoThuePhongTroWeb.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Services.YeuCauSuCos
{
    public interface IYeuCauSuCoService
    {
        Task<List<YeuCauSuCo>> GetAllAsync(int? chiNhanhId, TrangThaiSuCo? trangThai, int? soThang = 6);
        Task<List<YeuCauSuCo>> GetByNguoiThueAsync(int nguoiThueId);
        Task<YeuCauSuCo?> GetByIdAsync(int id);
        Task<bool> CreateAsync(YeuCauSuCo model);
        Task<bool> UpdateStatusAsync(int id, TrangThaiSuCo trangThai, double chiPhi, bool congVaoHoaDon, string? lyDoTuChoi, string? ghiChuAdmin);
        Task<bool> SoftDeleteAsync(int id);
    }
}
