using System.Collections.Generic;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Features.ThanhVienHopDongs.DTOs;

namespace QuanLyChoThuePhongTroWeb.Application.Features.ThanhVienHopDongs.Services
{
    public interface IThanhVienHopDongService
    {
        // Hợp đồng phải thuộc chi nhánh được phân công của actor.
        Task<List<ThanhVienHopDongRes>> GetThanhVienByHopDongIdAsync(int actorId, int hopDongId);
        Task<ServiceResult> AddThanhVienVaoHopDongAsync(int actorId, ThanhVienHopDongReq request);
        Task<ServiceResult> BaoRoiPhongAsync(int actorId, int chiTietId);
        Task<ServiceResult> XoaThanhVienNhamAsync(int actorId, int chiTietId);
    }
}
