using System.Collections.Generic;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Features.DichVus.DTOs;

namespace QuanLyChoThuePhongTroWeb.Application.Features.DichVus.Services
{
    public interface IDichVuService
    {
        // Danh mục dịch vụ dùng chung: ai cũng xem được, chỉ Admin sửa.
        Task<DataTableResponse<DichVuRes>> GetDanhSachDichVuAsync(DataTableRequest request);
        Task<DichVuRes?> GetDichVuByIdAsync(int id);
        Task<ServiceResult> CreateDichVuAsync(int actorId, DichVuReq input);
        Task<ServiceResult> UpdateDichVuAsync(int actorId, int id, DichVuReq input);
        Task<ServiceResult> DeleteDichVuAsync(int actorId, int id);

        // Bảng giá theo chi nhánh: chỉ thao tác trong chi nhánh được phân công.
        Task<DataTableResponse<DichVuChiNhanhRes>> GetDanhSachDichVuChiNhanhAsync(int actorId, DataTableRequest request, int chiNhanhId);
        Task<DichVuChiNhanhRes?> GetDichVuChiNhanhByIdAsync(int actorId, int id);
        Task<ServiceResult> CreateDichVuChiNhanhAsync(int actorId, DichVuChiNhanhReq input);
        Task<ServiceResult> UpdateDichVuChiNhanhAsync(int actorId, int id, DichVuChiNhanhReq input);
        Task<ServiceResult> DeleteDichVuChiNhanhAsync(int actorId, int id);

        // Đăng ký dịch vụ cho phòng: phòng phải thuộc chi nhánh được phân công; null khi không tìm thấy hoặc không có quyền.
        Task<List<DichVuChiNhanhWithDangKyRes>?> GetDichVuVaDangKyCuaPhongAsync(int actorId, int phongTroId);
        Task<ServiceResult> LuuDangKyDichVuAsync(int actorId, DangKyDichVuReq input);
    }
}
