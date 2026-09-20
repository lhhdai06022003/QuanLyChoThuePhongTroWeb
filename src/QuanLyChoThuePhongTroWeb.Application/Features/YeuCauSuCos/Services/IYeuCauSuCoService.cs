using System.Collections.Generic;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Common.Enums;
using QuanLyChoThuePhongTroWeb.Application.Features.YeuCauSuCos.DTOs;

namespace QuanLyChoThuePhongTroWeb.Application.Features.YeuCauSuCos.Services
{
    public interface IYeuCauSuCoService
    {
        Task<List<YeuCauSuCoRes>> GetAllAsync(int? chiNhanhId, AppTrangThaiSuCo? trangThai, int? soThang = 6);
        Task<List<YeuCauSuCoRes>> GetByNguoiThueAsync(int nguoiThueId);
        Task<YeuCauSuCoRes?> GetByIdAsync(int id);
        Task<bool> CreateAsync(CreateYeuCauSuCoReq req);
        Task<bool> UpdateStatusAsync(int id, AppTrangThaiSuCo trangThai, double chiPhi, bool congVaoHoaDon, string? lyDoTuChoi, string? ghiChuAdmin);
        Task<bool> SoftDeleteAsync(int id);
    }
}
