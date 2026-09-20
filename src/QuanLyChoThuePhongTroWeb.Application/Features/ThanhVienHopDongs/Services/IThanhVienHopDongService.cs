using System.Collections.Generic;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Features.ThanhVienHopDongs.DTOs;

namespace QuanLyChoThuePhongTroWeb.Application.Features.ThanhVienHopDongs.Services
{
    public interface IThanhVienHopDongService
    {
        Task<List<ThanhVienHopDongRes>> GetThanhVienByHopDongIdAsync(int hopDongId);
        Task<(bool IsSuccess, string? ErrorMessage)> AddThanhVienVaoHopDongAsync(ThanhVienHopDongReq request);
        Task<(bool IsSuccess, string? ErrorMessage)> BaoRoiPhongAsync(int chiTietId);
        Task<(bool IsSuccess, string? ErrorMessage)> XoaThanhVienNhamAsync(int chiTietId);
    }
}
