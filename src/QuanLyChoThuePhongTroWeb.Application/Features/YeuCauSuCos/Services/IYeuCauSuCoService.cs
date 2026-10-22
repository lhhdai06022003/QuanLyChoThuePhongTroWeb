using System.Threading;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Common.Enums;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Features.YeuCauSuCos.DTOs;

namespace QuanLyChoThuePhongTroWeb.Application.Features.YeuCauSuCos.Services
{
    public interface IYeuCauSuCoService
    {
        Task<List<YeuCauSuCoRes>> GetAllAsync(int? chiNhanhId, AppTrangThaiSuCo? trangThai, int? soThang = 6);
        Task<List<YeuCauSuCoRes>> GetByNguoiThueAsync(int nguoiThueId);
        Task<YeuCauSuCoRes?> GetByIdAsync(int id);
        Task<bool> CreateAsync(CreateYeuCauSuCoReq req);
        Task<ServiceResult> UpdateStatusAsync(int id, AppTrangThaiSuCo trangThai, decimal chiPhi, bool congVaoHoaDon, string? lyDoTuChoi, string? ghiChuAdmin, int actorId, CancellationToken ct = default);
        Task<ServiceResult> SoftDeleteAsync(int id, int actorId, CancellationToken ct = default);
    }
}
