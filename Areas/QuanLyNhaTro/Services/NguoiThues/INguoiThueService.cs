

using Microsoft.AspNetCore.Mvc.Rendering;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels.Requests;
using QuanLyChoThuePhongTroWeb.Models;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Services.NguoiThues
{
    public interface INguoiThueService
    {
        Task<IEnumerable<NguoiThue>> GetAllAsync();
        Task<IEnumerable<NguoiThue>> GetAvailableAsync();
        Task<NguoiThue> GetByIdAsync(int id);
        Task<(bool IsSuccess, string ErrorMessage)> CreateAsync(NguoiThueReq input);
        Task<(bool IsSuccess, string ErrorMessage)> UpdateAsync(int id, NguoiThueUpdateDto input);
        Task<(bool IsSuccess, string ErrorMessage)> DeleteAsync(int id);
        Task<List<SelectListItem>> DanhSachNguoiThue();
        Task<object> SearchAutocompleteAsync(string searchTerm);
        Task<(bool IsSuccess, string ErrorMessage)> PhatSinhNgauNhienAsync();
        

        // THÊM DÒNG NÀY: Khai báo hàm lấy danh sách người chưa thuê
        Task<List<SelectListItem>> DanhSachNguoiThueChuaCoPhong();
    }
}
