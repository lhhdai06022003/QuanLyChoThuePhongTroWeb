using System.Threading;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Features.ViecCanLam.DTOs;

namespace QuanLyChoThuePhongTroWeb.Application.Features.ViecCanLam.Services
{
    public interface IViecCanLamService
    {
        // Việc suy ra từ dữ liệu, giới hạn theo chi nhánh của actor; không lọc theo tháng/năm.
        Task<ViecCanLamTongHopDto> GetTongHopAsync(int actorId, int? branchId, CancellationToken ct = default);
        Task<DanhSachHoaDonQuaHanDto> GetHoaDonQuaHanAsync(int actorId, int? branchId, CancellationToken ct = default);
    }
}
