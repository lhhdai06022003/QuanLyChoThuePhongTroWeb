using System.Collections.Generic;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Features.NguoiThues.DTOs;

namespace QuanLyChoThuePhongTroWeb.Application.Features.NguoiThues.Services
{
    public interface INguoiThueService
    {
        Task<IEnumerable<NguoiThueRes>> GetAllAsync();
        Task<IEnumerable<NguoiThueRes>> GetAvailableAsync();
        Task<NguoiThueRes?> GetByIdAsync(int id);
        Task<(bool IsSuccess, string? ErrorMessage)> CreateAsync(NguoiThueReq input);
        Task<(bool IsSuccess, string? ErrorMessage)> UpdateAsync(int id, NguoiThueUpdateDto input);
        Task<(bool IsSuccess, string? ErrorMessage)> DeleteAsync(int id);
        Task<IReadOnlyList<SelectOptionDto>> DanhSachNguoiThue();
        Task<IReadOnlyList<NguoiThueAutocompleteDto>> SearchAutocompleteAsync(string searchTerm);
        Task<(bool IsSuccess, string? ErrorMessage)> PhatSinhNgauNhienAsync();
        Task<IReadOnlyList<SelectOptionDto>> DanhSachNguoiThueChuaCoPhong();
        Task<IReadOnlyList<SelectOptionDto>> DanhSachNguoiThueCoHopDongAsync();
    }
}
