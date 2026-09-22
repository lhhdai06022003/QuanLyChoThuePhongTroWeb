using System.Threading;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Domain.Entities;

namespace QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Persistence
{
    public interface IInvoicePaymentStore
    {
        Task<HoaDon?> GetHoaDonByIdAsync(int hoaDonId, CancellationToken cancellationToken = default);
        Task<YeuCauThanhToanHoaDon?> GetYeuCauThanhToanByIdAsync(int yeuCauId, CancellationToken cancellationToken = default);
        Task<MinhChungThanhToanHoaDon?> GetMinhChungByIdWithDetailsAsync(int minhChungId, CancellationToken cancellationToken = default);
        Task<decimal> GetTongTienDaThanhToanHoaDonAsync(int hoaDonId, CancellationToken cancellationToken = default);

        Task AddYeuCauThanhToanAsync(YeuCauThanhToanHoaDon yeuCau, CancellationToken cancellationToken = default);
        Task AddMinhChungThanhToanAsync(MinhChungThanhToanHoaDon minhChung, CancellationToken cancellationToken = default);
        Task AddLichSuThanhToanAsync(LichSuThanhToan lichSu, CancellationToken cancellationToken = default);
        Task AddLichSuTrangThaiYeuCauAsync(LichSuTrangThaiYeuCauThanhToanHoaDon lichSu, CancellationToken cancellationToken = default);
        Task AddLichSuTrangThaiHoaDonAsync(LichSuTrangThaiHoaDon lichSu, CancellationToken cancellationToken = default);

        void UpdateYeuCauThanhToan(YeuCauThanhToanHoaDon yeuCau);
        void UpdateMinhChungThanhToan(MinhChungThanhToanHoaDon minhChung);
        void UpdateHoaDon(HoaDon hoaDon);
    }
}
