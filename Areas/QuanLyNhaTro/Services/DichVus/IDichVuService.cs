using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels.Requests;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Services.DichVus
{
    public interface IDichVuService
    {
        // CRUD DichVu
        Task<DataTableResponse<DichVuRes>> GetDanhSachDichVuAsync(DataTableRequest request);
        Task<DichVuRes> GetDichVuByIdAsync(int id);
        Task<(bool IsSuccess, string ErrorMessage)> CreateDichVuAsync(DichVuReq input);
        Task<(bool IsSuccess, string ErrorMessage)> UpdateDichVuAsync(int id, DichVuReq input);
        Task<(bool IsSuccess, string ErrorMessage)> DeleteDichVuAsync(int id);

        // CRUD DichVuChiNhanh
        Task<DataTableResponse<DichVuChiNhanhRes>> GetDanhSachDichVuChiNhanhAsync(DataTableRequest request, int chiNhanhId);
        Task<DichVuChiNhanhRes> GetDichVuChiNhanhByIdAsync(int id);
        Task<(bool IsSuccess, string ErrorMessage)> CreateDichVuChiNhanhAsync(DichVuChiNhanhReq input);
        Task<(bool IsSuccess, string ErrorMessage)> UpdateDichVuChiNhanhAsync(int id, DichVuChiNhanhReq input);
        Task<(bool IsSuccess, string ErrorMessage)> DeleteDichVuChiNhanhAsync(int id);
    }
}
