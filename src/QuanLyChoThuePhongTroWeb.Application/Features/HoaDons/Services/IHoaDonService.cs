using System.Collections.Generic;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.DTOs;

namespace QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Services
{
    public interface IHoaDonService
    {
        Task<PhatSinhHoaDonResult> PhatSinhHoaDonAsync(int chiNhanhId, int thang, int nam, List<int> selectedPhongTroIds, int actorId = 0);
        Task<List<PhatSinhPreviewRes>> PreviewPhatSinhHoaDonAsync(int chiNhanhId, int thang, int nam, int actorId);
        Task<(bool IsSuccess, string? ErrorMessage)> UpdateHoaDonAsync(int hoaDonId, UpdateHoaDonReq req, int actorId = 0);
        Task<DataTableResponse<HoaDonRes>> GetDanhSachHoaDonAsync(DataTableRequest request, int chiNhanhId, int thang, int nam, int trangThai);
        Task<DataTableResponse<HoaDonRes>> GetEmployeeInvoiceListAsync(DataTableRequest request, int chiNhanhId, int thang, int nam, int trangThai, int actorId);
        Task<HoaDonChiTietRes?> GetHoaDonByIdAsync(int id);
        Task<HoaDonChiTietRes?> GetEmployeeInvoiceDetailAsync(int id, int actorId);
        Task<(bool IsSuccess, string? ErrorMessage)> ThuTienAsync(int hoaDonId, int phuongThuc, string ghiChu, int nguoiXacNhanId);
        Task<(bool IsSuccess, string? ErrorMessage)> DeleteHoaDonAsync(int id, int actorId, string lyDo, CancellationToken cancellationToken = default);
        Task<byte[]?> ExportExcelAsync(int hoaDonId);
        Task<byte[]?> ExportEmployeeExcelAsync(int hoaDonId, int actorId);
        Task<byte[]?> ExportPdfAsync(int hoaDonId);
        Task<byte[]?> ExportEmployeePdfAsync(int hoaDonId, int actorId);
        Task<List<HoaDonRes>> GetDanhSachHoaDonChuaThanhToanAsync(int chiNhanhId, int thang, int nam);
        Task<List<HoaDonRes>> GetEmployeeUnpaidInvoicesAsync(int chiNhanhId, int thang, int nam, int actorId);
        Task<List<HoaDonRes>> GetHoaDonsByNguoiThueIdAsync(int nguoiThueId);
        Task<bool> CheckHoaDonOwnershipAsync(int hoaDonId, int nguoiThueId);
        Task<(bool IsSuccess, string? ErrorMessage)> CheckInvoicePermissionAsync(int hoaDonId, int actorId, string actionCode);
        Task<SendInvoiceEmailResult> SendInvoiceEmailAsync(int hoaDonId, int actorId, CancellationToken cancellationToken = default);
    }
}
