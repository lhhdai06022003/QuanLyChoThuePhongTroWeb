using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels.Requests;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels.Responses;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Services.DieuKhoanMaus
{
    public interface IDieuKhoanMauService
    {
        Task<List<DieuKhoanMauRes>> GetAllAsync();
        Task<DieuKhoanMauRes> GetByIdAsync(int id);
        Task<ServiceResult> CreateAsync(DieuKhoanMauReq request);
        Task<ServiceResult> UpdateAsync(DieuKhoanMauReq request);
        Task<ServiceResult> DeleteAsync(int id);
    }
}
