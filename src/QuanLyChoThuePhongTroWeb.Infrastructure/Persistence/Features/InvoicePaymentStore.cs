using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Persistence;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Features
{
    public class InvoicePaymentStore : IInvoicePaymentStore
    {
        private readonly ApplicationDbContext _context;

        public InvoicePaymentStore(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<HoaDon?> GetHoaDonByIdAsync(int hoaDonId, CancellationToken cancellationToken = default)
        {
            return await _context.HoaDons
                .FirstOrDefaultAsync(h => h.HoaDonId == hoaDonId && !h.IsDeleted, cancellationToken);
        }

        public async Task<YeuCauThanhToanHoaDon?> GetYeuCauThanhToanByIdAsync(int yeuCauId, CancellationToken cancellationToken = default)
        {
            return await _context.YeuCauThanhToanHoaDons
                .Include(y => y.HoaDon)
                .FirstOrDefaultAsync(y => y.YeuCauThanhToanHoaDonId == yeuCauId && !y.IsDeleted, cancellationToken);
        }

        public async Task<MinhChungThanhToanHoaDon?> GetMinhChungByIdWithDetailsAsync(int minhChungId, CancellationToken cancellationToken = default)
        {
            return await _context.MinhChungThanhToanHoaDons
                .Include(m => m.YeuCauThanhToanHoaDon)
                    .ThenInclude(y => y.HoaDon)
                .FirstOrDefaultAsync(m => m.MinhChungThanhToanHoaDonId == minhChungId && !m.IsDeleted, cancellationToken);
        }

        public async Task<decimal> GetTongTienDaThanhToanHoaDonAsync(int hoaDonId, CancellationToken cancellationToken = default)
        {
            return await _context.LichSuThanhToans
                .Where(l => l.HoaDonId == hoaDonId && !l.IsDeleted)
                .SumAsync(l => l.SoTienThanhToan, cancellationToken);
        }

        public async Task AddYeuCauThanhToanAsync(YeuCauThanhToanHoaDon yeuCau, CancellationToken cancellationToken = default)
        {
            await _context.YeuCauThanhToanHoaDons.AddAsync(yeuCau, cancellationToken);
        }

        public async Task AddMinhChungThanhToanAsync(MinhChungThanhToanHoaDon minhChung, CancellationToken cancellationToken = default)
        {
            await _context.MinhChungThanhToanHoaDons.AddAsync(minhChung, cancellationToken);
        }

        public async Task AddLichSuThanhToanAsync(LichSuThanhToan lichSu, CancellationToken cancellationToken = default)
        {
            await _context.LichSuThanhToans.AddAsync(lichSu, cancellationToken);
        }

        public async Task AddLichSuTrangThaiYeuCauAsync(LichSuTrangThaiYeuCauThanhToanHoaDon lichSu, CancellationToken cancellationToken = default)
        {
            await _context.LichSuTrangThaiYeuCauThanhToanHoaDons.AddAsync(lichSu, cancellationToken);
        }

        public async Task AddLichSuTrangThaiHoaDonAsync(LichSuTrangThaiHoaDon lichSu, CancellationToken cancellationToken = default)
        {
            await _context.LichSuTrangThaiHoaDons.AddAsync(lichSu, cancellationToken);
        }

        public void UpdateYeuCauThanhToan(YeuCauThanhToanHoaDon yeuCau)
        {
            _context.YeuCauThanhToanHoaDons.Update(yeuCau);
        }

        public void UpdateMinhChungThanhToan(MinhChungThanhToanHoaDon minhChung)
        {
            _context.MinhChungThanhToanHoaDons.Update(minhChung);
        }

        public void UpdateHoaDon(HoaDon hoaDon)
        {
            _context.HoaDons.Update(hoaDon);
        }
    }
}
