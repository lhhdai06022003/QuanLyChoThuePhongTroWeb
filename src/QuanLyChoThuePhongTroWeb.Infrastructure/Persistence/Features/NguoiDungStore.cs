using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using QuanLyChoThuePhongTroWeb.Application.Features.NguoiDungs.Persistence;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Features
{
    public class NguoiDungStore : INguoiDungStore
    {
        private readonly ApplicationDbContext _context;

        public NguoiDungStore(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<NguoiDung?> GetActiveByUsernameAsync(string username, CancellationToken cancellationToken = default)
        {
            return await _context.NguoiDungs
                .FirstOrDefaultAsync(x => x.TenDangNhap == username && !x.IsDeleted, cancellationToken);
        }

        public async Task<NguoiDung?> GetActiveWithTenantByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            return await _context.NguoiDungs
                .Include(x => x.NguoiThue)
                .FirstOrDefaultAsync(x => x.NguoiDungId == id && !x.IsDeleted, cancellationToken);
        }

        public async Task<IReadOnlyList<NguoiDung>> GetAllActiveWithTenantAsync(CancellationToken cancellationToken = default)
        {
            return await _context.NguoiDungs
                .Where(x => !x.IsDeleted)
                .Include(x => x.NguoiThue)
                .ToListAsync(cancellationToken);
        }

        public async Task<bool> ExistsActiveUsernameAsync(string username, CancellationToken cancellationToken = default)
        {
            return await _context.NguoiDungs
                .AnyAsync(x => !x.IsDeleted && x.TenDangNhap == username, cancellationToken);
        }

        public async Task<bool> ExistsActiveTenantAccountAsync(int nguoiThueId, CancellationToken cancellationToken = default)
        {
            return await _context.NguoiDungs
                .AnyAsync(x => !x.IsDeleted && x.NguoiThueId == nguoiThueId, cancellationToken);
        }

        public async Task<bool> ExistsActiveTenantAccountAsync(int nguoiThueId, int excludeUserId, CancellationToken cancellationToken = default)
        {
            return await _context.NguoiDungs
                .AnyAsync(x => x.NguoiDungId != excludeUserId && !x.IsDeleted && x.NguoiThueId == nguoiThueId, cancellationToken);
        }

        public async Task<NguoiDung?> GetSoftDeletedByTenantIdAsync(int nguoiThueId, CancellationToken cancellationToken = default)
        {
            return await _context.NguoiDungs
                .FirstOrDefaultAsync(x => x.IsDeleted && x.NguoiThueId == nguoiThueId, cancellationToken);
        }

        public async Task<NguoiDung?> GetSoftDeletedByUsernameAsync(string username, CancellationToken cancellationToken = default)
        {
            return await _context.NguoiDungs
                .FirstOrDefaultAsync(x => x.IsDeleted && x.TenDangNhap == username, cancellationToken);
        }

        public async Task<NguoiDung?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            return await _context.NguoiDungs.FindAsync(new object[] { id }, cancellationToken);
        }

        public async Task AddAsync(NguoiDung nguoiDung, CancellationToken cancellationToken = default)
        {
            await _context.NguoiDungs.AddAsync(nguoiDung, cancellationToken);
        }

        public void Update(NguoiDung nguoiDung)
        {
            _context.NguoiDungs.Update(nguoiDung);
        }
    }
}
