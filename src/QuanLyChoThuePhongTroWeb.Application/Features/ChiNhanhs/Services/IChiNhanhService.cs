using System.Collections.Generic;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Features.ChiNhanhs.DTOs;

namespace QuanLyChoThuePhongTroWeb.Application.Features.ChiNhanhs.Services
{
    public interface IChiNhanhService
    {
        Task<IEnumerable<ChiNhanhRes>> DanhSachChiNhanh();
        Task<ChiNhanhRes?> GetChiNhanh(int id);
        Task<(bool IsSuccess, string Message)> ThemChiNhanhMoi(ChiNhanhReq req);
        Task<(bool IsSuccess, string Message)> XoaChiNhanh(int id);
    }
}
