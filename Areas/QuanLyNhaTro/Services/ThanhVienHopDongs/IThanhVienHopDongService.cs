using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels.Requests;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels.Responses;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Services.ThanhVienHopDongs
{
    public interface IThanhVienHopDongService
    {
        Task<List<ThanhVienHopDongRes>> GetThanhVienByHopDongIdAsync(int hopDongId);
        Task<(bool IsSuccess, string ErrorMessage)> AddThanhVienVaoHopDongAsync(ThanhVienHopDongReq request);
        Task<(bool IsSuccess, string ErrorMessage)> BaoRoiPhongAsync(int chiTietId);
        Task<(bool IsSuccess, string ErrorMessage)> XoaThanhVienNhamAsync(int chiTietId);
    }
}
