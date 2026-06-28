using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels.Requests;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels.Responses;
using QuanLyChoThuePhongTroWeb.Models;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Services.HopDongs
{
    public interface IHopDongService
    {
        Task<DataTableResponse<HopDongRes>> DanhSachHopDongSideAsync(HopDongFilterReq request);
        Task<HopDongDetailRes> GetByIdAsync(int id);
        Task<(bool IsSuccess, string ErrorMessage)> CreateAsync(HopDongReq input);
        Task<(bool IsSuccess, string ErrorMessage)> UpdateAsync(int id, HopDongReq input);
        Task<(bool IsSuccess, string ErrorMessage)> DeleteAsync(int id);
        Task<HopDongPrintRes> GetPrintDataAsync(int id);
        Task<IEnumerable<HopDong>> GetHopDongsByNguoiThueIdAsync(int nguoiThueId);
        Task<object> GetChiTietHopDongKhachThueAsync(int id, int nguoiThueId);
    }
}
