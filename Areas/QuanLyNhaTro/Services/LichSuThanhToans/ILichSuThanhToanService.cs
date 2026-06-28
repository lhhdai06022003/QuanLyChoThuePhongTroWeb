using System;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels.Responses;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels.Requests;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Services.LichSuThanhToans
{
    public interface ILichSuThanhToanService
    {
        Task<DataTableResponse<LichSuThanhToanGiaoDichRes>> GetDanhSachThanhToanAsync(
            DataTableRequest request, int chiNhanhId, int phuongThuc, DateTime? tuNgay, DateTime? denNgay);

        Task<ThongKeThanhToanRes> GetThongKeThanhToanAsync(int chiNhanhId, DateTime? tuNgay, DateTime? denNgay);

        Task<(bool IsSuccess, string ErrorMessage)> HuyGiaoDichAsync(int id, int adminUserId);

        Task<System.Collections.Generic.IEnumerable<QuanLyChoThuePhongTroWeb.Models.LichSuThanhToan>> GetLichSuByNguoiThueIdAsync(int nguoiThueId);
    }
}
