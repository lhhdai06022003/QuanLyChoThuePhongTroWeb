using Microsoft.AspNetCore.Mvc.Rendering;
using QuanLyChoThuePhongTroWeb.Models;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLy.Services.PhongTroService
{
    public interface IPhongTroService
    {
        Task<List<SelectListItem>> GetDanhSachChiNhanhDropdownAsync();
        Task<object> GetDanhSachPhongTroAsync();
        Task<ServiceResult> GetPhongTroByIdAsync(int id);
        Task<ServiceResult> ThemPhongTroAsync(PhongTro model);
        Task<ServiceResult> CapNhatPhongTroAsync(PhongTro model);
        Task<ServiceResult> XoaPhongTroAsync(int id);
    }
}
