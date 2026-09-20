using System.Collections.Generic;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Features.PhongTros.DTOs;

namespace QuanLyChoThuePhongTroWeb.Application.Features.PhongTros.Services
{
    public interface IPhongTroService
    {
        Task<IReadOnlyList<SelectOptionDto>> GetDanhSachChiNhanhDropdownAsync();
        Task<IReadOnlyList<PhongTroListItemDto>> GetDanhSachPhongTroAsync();
        Task<ServiceResult> GetPhongTroByIdAsync(int id);
        Task<ServiceResult> ThemPhongTroAsync(PhongTroReq model);
        Task<ServiceResult> CapNhatPhongTroAsync(PhongTroReq model);
        Task<ServiceResult> XoaPhongTroAsync(int id);
        Task<List<PhongTroOptionDto>> DanhSachPhongTroConTrong();
        Task<List<PhongCardRes>> GetSoDoPhongAsync(int chiNhanhId);
        Task<QuickContractDto?> GetQuickContractAsync(int phongTroId);
        Task<UnpaidInvoiceDto?> GetUnpaidInvoiceAsync(int phongTroId);
        Task<ServiceResult> PhatSinhNgauNhienAsync();
    }
}
