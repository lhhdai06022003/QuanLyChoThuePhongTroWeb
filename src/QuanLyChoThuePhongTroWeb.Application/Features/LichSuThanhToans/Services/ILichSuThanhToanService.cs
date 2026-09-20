using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Features.LichSuThanhToans.DTOs;
namespace QuanLyChoThuePhongTroWeb.Application.Features.LichSuThanhToans.Services
{
    public interface ILichSuThanhToanService
    {
        Task<DataTableResponse<LichSuThanhToanGiaoDichRes>> GetDanhSachThanhToanAsync(
            DataTableRequest request, int chiNhanhId, int phuongThuc, DateTime? tuNgay, DateTime? denNgay);

        Task<ThongKeThanhToanRes> GetThongKeThanhToanAsync(int chiNhanhId, DateTime? tuNgay, DateTime? denNgay);

        Task<(bool IsSuccess, string? ErrorMessage)> HuyGiaoDichAsync(int id, int adminUserId);

        Task<IReadOnlyList<LichSuThanhToanKhachThueDto>> GetLichSuByNguoiThueIdAsync(int nguoiThueId);
    }
}
