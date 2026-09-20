using System.Collections.Generic;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Common.Enums;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Features.NguoiDungs.DTOs;

namespace QuanLyChoThuePhongTroWeb.Application.Features.NguoiDungs.Services
{
    public interface INguoiDungService
    {
        Task<IEnumerable<NguoiDungRes>> GetAllAsync();
        Task<NguoiDungRes?> GetByIdAsync(int id);
        Task<ServiceResult> AddAsync(CreateNguoiDungReq req);
        Task<ServiceResult> DeleteAsync(int id);
        Task SeedAdminAccountAsync();
        Task<UserAuthDto?> ValidateUserAsync(string username, string password);
        Task<ServiceResult> UpdateUserAsync(int id, AppRole role, bool isActive, string? newPassword, int? nguoiThueId);
        Task<ServiceResult> ChangePasswordAsync(int userId, string oldPassword, string newPassword);
        Task<ServiceResult> ResetPasswordToPhoneAsync(int id);
    }
}
