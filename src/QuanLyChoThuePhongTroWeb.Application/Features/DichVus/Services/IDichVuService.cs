using System.Collections.Generic;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Features.DichVus.DTOs;

namespace QuanLyChoThuePhongTroWeb.Application.Features.DichVus.Services
{
    public interface IDichVuService
    {
        // CRUD DichVu
        Task<DataTableResponse<DichVuRes>> GetDanhSachDichVuAsync(DataTableRequest request);
        Task<DichVuRes?> GetDichVuByIdAsync(int id);
        Task<(bool IsSuccess, string? ErrorMessage)> CreateDichVuAsync(DichVuReq input);
        Task<(bool IsSuccess, string? ErrorMessage)> UpdateDichVuAsync(int id, DichVuReq input);
        Task<(bool IsSuccess, string? ErrorMessage)> DeleteDichVuAsync(int id);

        // CRUD DichVuChiNhanh
        Task<DataTableResponse<DichVuChiNhanhRes>> GetDanhSachDichVuChiNhanhAsync(DataTableRequest request, int chiNhanhId);
        Task<DichVuChiNhanhRes?> GetDichVuChiNhanhByIdAsync(int id);
        Task<(bool IsSuccess, string? ErrorMessage)> CreateDichVuChiNhanhAsync(DichVuChiNhanhReq input);
        Task<(bool IsSuccess, string? ErrorMessage)> UpdateDichVuChiNhanhAsync(int id, DichVuChiNhanhReq input);
        Task<(bool IsSuccess, string? ErrorMessage)> DeleteDichVuChiNhanhAsync(int id);

        // Đăng ký dịch vụ cho phòng
        Task<List<DichVuChiNhanhWithDangKyRes>> GetDichVuVaDangKyCuaPhongAsync(int phongTroId);
        Task<(bool IsSuccess, string? ErrorMessage)> LuuDangKyDichVuAsync(DangKyDichVuReq input);
    }
}
