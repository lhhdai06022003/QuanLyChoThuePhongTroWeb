using QuanLyChoThuePhongTroWeb.Models;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Services.NguoiDungs
{
    public interface INguoiDungService
    {
        Task<IEnumerable<NguoiDung>> GetAllAsync();
        Task<NguoiDung?> GetByIdAsync(int id);
        Task<ServiceResult> AddAsync(NguoiDung nguoiDung);
        Task<ServiceResult> UpdateAsync(NguoiDung nguoiDung);
        Task<ServiceResult> DeleteAsync(int id);
        Task SeedAdminAccountAsync();
        Task<NguoiDung?> ValidateUserAsync(string username, string password);
        Task<ServiceResult> UpdateUserAsync(int id, Role role, bool isActive, string? newPassword, int? nguoiThueId);
        Task<ServiceResult> ChangePasswordAsync(int userId, string oldPassword, string newPassword);
        Task<ServiceResult> ResetPasswordToPhoneAsync(int id);
    }
}