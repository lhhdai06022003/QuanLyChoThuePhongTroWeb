using System.Collections.Generic;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Features.PhongTros.DTOs;

namespace QuanLyChoThuePhongTroWeb.Application.Features.PhongTros.Services
{
    // Mọi thao tác nhận actorId để giới hạn theo chi nhánh được phân công: Admin thấy tất cả,
    // nhân viên chỉ thấy chi nhánh của mình; thêm, xóa và phát sinh phòng chỉ dành cho Admin.
    public interface IPhongTroService
    {
        Task<IReadOnlyList<SelectOptionDto>> GetDanhSachChiNhanhDropdownAsync(int actorId);
        Task<IReadOnlyList<PhongTroListItemDto>> GetDanhSachPhongTroAsync(int actorId);
        Task<ServiceResult> GetPhongTroByIdAsync(int actorId, int id);
        Task<ServiceResult> ThemPhongTroAsync(int actorId, PhongTroReq model);
        Task<ServiceResult> CapNhatPhongTroAsync(int actorId, PhongTroReq model);
        Task<ServiceResult> XoaPhongTroAsync(int actorId, int id);
        Task<List<PhongTroOptionDto>> DanhSachPhongTroConTrong(int actorId);
        Task<List<PhongCardRes>> GetSoDoPhongAsync(int actorId, int chiNhanhId);
        Task<QuickContractDto?> GetQuickContractAsync(int actorId, int phongTroId);
        Task<UnpaidInvoiceDto?> GetUnpaidInvoiceAsync(int actorId, int phongTroId);
        Task<ServiceResult> PhatSinhNgauNhienAsync(int actorId);
    }
}
