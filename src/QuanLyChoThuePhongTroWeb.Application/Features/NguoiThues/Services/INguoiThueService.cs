using System.Collections.Generic;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Features.NguoiThues.DTOs;

namespace QuanLyChoThuePhongTroWeb.Application.Features.NguoiThues.Services
{
    // Phía quản lý: mọi thao tác nhận actorId; nhân viên chỉ thấy người thuê có hợp đồng tại chi nhánh
    // được phân công hoặc chưa có hợp đồng nào. GetByIdAsync(id) dành cho cổng khách thuê (đã gắn với
    // NguoiThueId của tài khoản) nên không giới hạn chi nhánh.
    public interface INguoiThueService
    {
        Task<IEnumerable<NguoiThueRes>> GetAllAsync(int actorId);
        Task<IEnumerable<NguoiThueRes>> GetAvailableAsync(int actorId);
        Task<NguoiThueRes?> GetByIdAsync(int id);
        Task<NguoiThueRes?> GetByIdForStaffAsync(int actorId, int id);
        Task<ServiceResult> CreateAsync(int actorId, NguoiThueReq input);
        Task<ServiceResult> UpdateAsync(int actorId, int id, NguoiThueUpdateDto input);
        Task<ServiceResult> DeleteAsync(int actorId, int id);
        Task<IReadOnlyList<SelectOptionDto>> DanhSachNguoiThue(int actorId);
        Task<IReadOnlyList<NguoiThueAutocompleteDto>> SearchAutocompleteAsync(int actorId, string searchTerm);
        Task<ServiceResult> PhatSinhNgauNhienAsync(int actorId);
        Task<IReadOnlyList<SelectOptionDto>> DanhSachNguoiThueChuaCoPhong(int actorId);
        Task<IReadOnlyList<SelectOptionDto>> DanhSachNguoiThueCoHopDongAsync(int actorId);
    }
}
