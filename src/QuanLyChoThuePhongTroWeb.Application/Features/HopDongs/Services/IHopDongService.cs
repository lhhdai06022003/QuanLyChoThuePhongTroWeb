using System.Collections.Generic;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Features.HopDongs.DTOs;
namespace QuanLyChoThuePhongTroWeb.Application.Features.HopDongs.Services
{
    // Các thao tác phía quản lý nhận actorId và chỉ chạy trên hợp đồng thuộc chi nhánh được phân công.
    // Hai hàm cuối phục vụ cổng khách thuê, đã giới hạn theo nguoiThueId nên không nhận actorId.
    public interface IHopDongService
    {
        Task<DataTableResponse<HopDongRes>> DanhSachHopDongSideAsync(int actorId, HopDongFilterReq request);
        Task<HopDongDetailRes?> GetByIdAsync(int actorId, int id);
        Task<ServiceResult> CreateAsync(int actorId, HopDongReq input);
        Task<ServiceResult> UpdateAsync(int actorId, int id, HopDongReq input);
        Task<ServiceResult> DeleteAsync(int actorId, int id);
        Task<HopDongPrintRes?> GetPrintDataAsync(int actorId, int id);
        Task<IReadOnlyList<HopDongRes>> GetHopDongsByNguoiThueIdAsync(int nguoiThueId);
        Task<HopDongKhachThueDetailDto?> GetChiTietHopDongKhachThueAsync(int id, int nguoiThueId);
    }
}
