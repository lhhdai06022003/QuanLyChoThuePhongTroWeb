using System.Collections.Generic;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Features.HopDongs.DTOs;
namespace QuanLyChoThuePhongTroWeb.Application.Features.HopDongs.Services
{
    public interface IHopDongService
    {
        Task<DataTableResponse<HopDongRes>> DanhSachHopDongSideAsync(HopDongFilterReq request);
        Task<HopDongDetailRes?> GetByIdAsync(int id);
        Task<(bool IsSuccess, string? ErrorMessage)> CreateAsync(HopDongReq input);
        Task<(bool IsSuccess, string? ErrorMessage)> UpdateAsync(int id, HopDongReq input);
        Task<(bool IsSuccess, string? ErrorMessage)> DeleteAsync(int id);
        Task<HopDongPrintRes?> GetPrintDataAsync(int id);
        Task<IReadOnlyList<HopDongRes>> GetHopDongsByNguoiThueIdAsync(int nguoiThueId);
        Task<HopDongKhachThueDetailDto?> GetChiTietHopDongKhachThueAsync(int id, int nguoiThueId);
    }
}
