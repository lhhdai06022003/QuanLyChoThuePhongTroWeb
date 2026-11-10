using System.Collections.Generic;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Features.LichSuThanhToans.DTOs;
namespace QuanLyChoThuePhongTroWeb.Application.Features.LichSuThanhToans.Services
{
    public interface ILichSuThanhToanService
    {
        Task<DataTableResponse<LichSuThanhToanGiaoDichRes>> GetDanhSachThanhToanAsync(int actorId, DataTableRequest request, LichSuThanhToanLocReq loc);

        Task<ThongKeThanhToanRes> GetThongKeThanhToanAsync(int actorId, LichSuThanhToanLocReq loc);

        Task<IReadOnlyList<LichSuThanhToanKhachThueDto>> GetLichSuByNguoiThueIdAsync(int nguoiThueId);
    }
}
