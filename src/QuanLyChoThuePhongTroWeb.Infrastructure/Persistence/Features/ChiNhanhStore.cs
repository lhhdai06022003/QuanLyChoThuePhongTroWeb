using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using QuanLyChoThuePhongTroWeb.Application.Features.ChiNhanhs.Persistence;
using QuanLyChoThuePhongTroWeb.Domain.Entities;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Features
{
    public class ChiNhanhStore : IChiNhanhStore
    {
        private readonly ApplicationDbContext _context;

        public ChiNhanhStore(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IReadOnlyList<ChiNhanh>> GetAllActiveAsync(CancellationToken cancellationToken = default)
        {
            return await _context.ChiNhanhs
                .Where(c => !c.IsDeleted)
                .OrderByDescending(c => c.NgayTao)
                .ToListAsync(cancellationToken);
        }

        public async Task<ChiNhanh?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            return await _context.ChiNhanhs
                .FirstOrDefaultAsync(c => c.ChiNhanhId == id && !c.IsDeleted, cancellationToken);
        }

        public async Task<bool> ExistsMaAsync(string maChiNhanh, int excludeId = 0, CancellationToken cancellationToken = default)
        {
            string normMa = maChiNhanh.Trim().ToUpper();
            return await _context.ChiNhanhs
                .AnyAsync(c => c.MaChiNhanh.ToUpper() == normMa && c.ChiNhanhId != excludeId && !c.IsDeleted, cancellationToken);
        }

        public void Add(ChiNhanh chiNhanh)
        {
            _context.ChiNhanhs.Add(chiNhanh);
        }

        public void Update(ChiNhanh chiNhanh)
        {
            _context.ChiNhanhs.Update(chiNhanh);
        }
    }
}
