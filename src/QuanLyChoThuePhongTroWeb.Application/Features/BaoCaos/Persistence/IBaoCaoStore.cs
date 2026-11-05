using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace QuanLyChoThuePhongTroWeb.Application.Features.BaoCaos.Persistence
{
    // allowedBranchIds: null = không giới hạn (Admin); rỗng = không thấy gì.
    public interface IBaoCaoStore
    {
        Task<string?> GetTenChiNhanhAsync(int chiNhanhId, CancellationToken cancellationToken = default);

        // Hóa đơn đã gửi (DaGui, chưa xóa) có kỳ Nam*100+Thang nằm trong khoảng.
        Task<IReadOnlyList<BaoCaoHoaDonRow>> GetHoaDonDaGuiAsync(int? chiNhanhId, IReadOnlyCollection<int>? allowedBranchIds, int tuKyKey, int denKyKey, CancellationToken cancellationToken = default);

        // Dòng sổ chưa xóa mềm của các hóa đơn trên.
        Task<IReadOnlyList<BaoCaoThanhToanRow>> GetThanhToanCuaHoaDonDaGuiAsync(int? chiNhanhId, IReadOnlyCollection<int>? allowedBranchIds, int tuKyKey, int denKyKey, CancellationToken cancellationToken = default);

        Task<decimal> GetTienCocDangGiuAsync(int? chiNhanhId, IReadOnlyCollection<int>? allowedBranchIds, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<BaoCaoGiuChoRow>> GetGiuChoDaNhanTienAsync(int? chiNhanhId, IReadOnlyCollection<int>? allowedBranchIds, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<BaoCaoHoaDonNoRow>> GetHoaDonConNoAsync(int? chiNhanhId, IReadOnlyCollection<int>? allowedBranchIds, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<BaoCaoPhongRow>> GetPhongAsync(int? chiNhanhId, IReadOnlyCollection<int>? allowedBranchIds, CancellationToken cancellationToken = default);

        // Hợp đồng chưa xóa, không phải DaHuy; không lọc theo kỳ vì cần đủ chuỗi mã gia hạn.
        Task<IReadOnlyList<BaoCaoHopDongRow>> GetHopDongAsync(int? chiNhanhId, IReadOnlyCollection<int>? allowedBranchIds, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<BaoCaoDienNuocRow>> GetDienNuocDaDuyetAsync(int? chiNhanhId, IReadOnlyCollection<int>? allowedBranchIds, int tuKyKey, int denKyKey, CancellationToken cancellationToken = default);
    }
}
