using System.Collections.Generic;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Features.DieuKhoanMaus.DTOs;

namespace QuanLyChoThuePhongTroWeb.Application.Features.DieuKhoanMaus.Services
{
    public interface IDieuKhoanMauService
    {
        Task<List<DieuKhoanMauRes>> GetAllAsync();
        Task<DieuKhoanMauRes?> GetByIdAsync(int id);
        Task<ServiceResult> CreateAsync(DieuKhoanMauReq request);
        Task<ServiceResult> UpdateAsync(DieuKhoanMauReq request);
        Task<ServiceResult> DeleteAsync(int id);
    }
}
