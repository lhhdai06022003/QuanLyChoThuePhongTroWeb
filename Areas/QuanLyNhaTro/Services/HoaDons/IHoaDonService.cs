using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels.Requests;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Services.HoaDons
{
    public interface IHoaDonService
    {
        Task<(bool IsSuccess, string Message, int SoHoaDonMoi)> PhatSinhHoaDonAsync(int chiNhanhId, int thang, int nam);
        Task<DataTableResponse<HoaDonRes>> GetDanhSachHoaDonAsync(DataTableRequest request, int chiNhanhId, int thang, int nam, int trangThai);
        Task<HoaDonChiTietRes> GetHoaDonByIdAsync(int id);
        Task<(bool IsSuccess, string ErrorMessage)> ThuTienAsync(int hoaDonId, int phuongThuc, string ghiChu, int nguoiXacNhanId);
        Task<(bool IsSuccess, string ErrorMessage)> DeleteHoaDonAsync(int id);
        Task<byte[]> ExportExcelAsync(int hoaDonId);
        Task<byte[]> ExportPdfAsync(int hoaDonId);
    }
}
