using System.Collections.Generic;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.DTOs;

namespace QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.Services
{
    public interface IDienNuocService
    {
        Task<List<DienNuocPhongRes>> GetDanhSachDienNuocAsync(int chiNhanhId, int thang, int nam);
        Task<(bool IsSuccess, string? ErrorMessage)> SaveChotDienNuocAsync(ChotDienNuocReq input);
    }
}
