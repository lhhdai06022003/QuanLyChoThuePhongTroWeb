using QuanLyChoThuePhongTroWeb.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Services.NguoiDungs
{
    public interface INguoiDungService
    {
        Task<IEnumerable<NguoiDung>> GetAllAsync();
        Task<NguoiDung?> GetByIdAsync(int id);
        Task AddAsync(NguoiDung nguoiDung);
        Task UpdateAsync(NguoiDung nguoiDung);
        Task DeleteAsync(int id);
        Task SeedAdminAccountAsync();
        Task<NguoiDung?> ValidateUserAsync(string username, string password);
        Task<bool> UpdateUserAsync(int id, Role role, bool isActive, string? newPassword);
        Task<(bool IsSuccess, string Message)> ChangePasswordAsync(int userId, string oldPassword, string newPassword);
    }
}