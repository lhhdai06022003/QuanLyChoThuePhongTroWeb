using System.Collections.Generic;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.DTOs;

namespace QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Services
{
    public interface IHoaDonService
    {
        Task<PhatSinhHoaDonResult> PhatSinhHoaDonAsync(int chiNhanhId, int thang, int nam, List<int> selectedPhongTroIds);
        Task<List<PhatSinhPreviewRes>> PreviewPhatSinhHoaDonAsync(int chiNhanhId, int thang, int nam);
        Task<(bool IsSuccess, string? ErrorMessage)> UpdateHoaDonAsync(int hoaDonId, UpdateHoaDonReq req);
        Task<DataTableResponse<HoaDonRes>> GetDanhSachHoaDonAsync(DataTableRequest request, int chiNhanhId, int thang, int nam, int trangThai);
        Task<HoaDonChiTietRes?> GetHoaDonByIdAsync(int id);
        Task<(bool IsSuccess, string? ErrorMessage)> ThuTienAsync(int hoaDonId, int phuongThuc, string ghiChu, int nguoiXacNhanId);
        Task<(bool IsSuccess, string? ErrorMessage)> DeleteHoaDonAsync(int id);
        Task<byte[]?> ExportExcelAsync(int hoaDonId);
        Task<byte[]?> ExportPdfAsync(int hoaDonId);
        Task<List<HoaDonRes>> GetDanhSachHoaDonChuaThanhToanAsync(int chiNhanhId, int thang, int nam);
        Task<List<HoaDonRes>> GetHoaDonsByNguoiThueIdAsync(int nguoiThueId);
        Task<bool> CheckHoaDonOwnershipAsync(int hoaDonId, int nguoiThueId);
    }
}
