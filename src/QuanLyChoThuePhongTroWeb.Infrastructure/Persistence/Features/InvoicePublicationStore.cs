using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Persistence;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Features
{
    public class InvoicePublicationStore : IInvoicePublicationStore
    {
        private readonly ApplicationDbContext _context;

        public InvoicePublicationStore(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<InvoicePublicationSnapshot?> GetSnapshotAsync(int hoaDonId, CancellationToken ct = default)
        {
            return await _context.HoaDons
                .AsNoTracking()
                .Where(h => h.HoaDonId == hoaDonId)
                .Select(h => new InvoicePublicationSnapshot
                {
                    HoaDonId = h.HoaDonId,
                    MaHoaDon = h.MaHoaDon,
                    HopDongId = h.HopDongId,
                    ChiNhanhId = h.HopDong.PhongTro.ChiNhanhId,
                    TrangThaiPhatHanh = h.TrangThaiPhatHanh,
                    IsDeleted = h.IsDeleted
                })
                .FirstOrDefaultAsync(ct);
        }

        public async Task<HoaDon?> LockInvoiceAsync(int hoaDonId, CancellationToken ct = default)
        {
            _context.ChangeTracker.Clear();

            return await _context.HoaDons
                .FromSqlInterpolated($"SELECT * FROM hoa_don WHERE \"HoaDonId\" = {hoaDonId} FOR UPDATE")
                .FirstOrDefaultAsync(ct);
        }

        public async Task<InvoiceRecipientInfo?> GetRecipientAsync(int hopDongId, CancellationToken ct = default)
        {
            var hopDong = await _context.HopDongs
                .AsNoTracking()
                .Where(h => h.HopDongId == hopDongId)
                .Select(h => new
                {
                    h.NguoiThueId,
                    HoVaTen = h.NguoiThue.HoVaTen,
                    Email = h.NguoiThue.Email
                })
                .FirstOrDefaultAsync(ct);

            if (hopDong == null)
            {
                return null;
            }

            var nguoiDungId = await _context.NguoiDungs
                .AsNoTracking()
                .Where(u => u.NguoiThueId == hopDong.NguoiThueId && !u.IsDeleted && u.IsActive)
                .OrderBy(u => u.NguoiDungId)
                .Select(u => (int?)u.NguoiDungId)
                .FirstOrDefaultAsync(ct);

            var email = string.IsNullOrWhiteSpace(hopDong.Email) ? null : hopDong.Email.Trim();

            return new InvoiceRecipientInfo
            {
                NguoiThueId = hopDong.NguoiThueId,
                HoVaTen = hopDong.HoVaTen,
                Email = email,
                NguoiDungId = nguoiDungId
            };
        }

        public void AddNotification(ThongBao thongBao)
        {
            _context.ThongBaos.Add(thongBao);
        }
    }
}
