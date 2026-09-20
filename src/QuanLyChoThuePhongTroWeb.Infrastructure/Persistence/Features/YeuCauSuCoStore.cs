using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using QuanLyChoThuePhongTroWeb.Application.Features.YeuCauSuCos.Persistence;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Features
{
    public class YeuCauSuCoStore : IYeuCauSuCoStore
    {
        private readonly ApplicationDbContext _context;

        public YeuCauSuCoStore(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<YeuCauSuCo>> GetAllAsync(int? chiNhanhId, TrangThaiSuCo? trangThai, int? soThang = 6, CancellationToken cancellationToken = default)
        {
            var query = _context.YeuCauSuCos
                .Include(x => x.PhongTro).ThenInclude(p => p.ChiNhanh)
                .Include(x => x.NguoiThue)
                .Where(x => !x.IsDeleted);

            if (chiNhanhId.HasValue && chiNhanhId.Value > 0)
            {
                query = query.Where(x => x.PhongTro.ChiNhanhId == chiNhanhId.Value);
            }

            if (trangThai.HasValue)
            {
                query = query.Where(x => x.TrangThai == trangThai.Value);
            }

            if (soThang.HasValue && soThang.Value > 0)
            {
                var fromDate = DateTime.UtcNow.AddMonths(-soThang.Value);
                query = query.Where(x => x.NgayGui >= fromDate);
            }

            return await query.OrderByDescending(x => x.NgayGui).ToListAsync(cancellationToken);
        }

        public async Task<List<YeuCauSuCo>> GetByNguoiThueAsync(int nguoiThueId, CancellationToken cancellationToken = default)
        {
            return await _context.YeuCauSuCos
                .Include(x => x.PhongTro)
                .Where(x => x.NguoiThueId == nguoiThueId && !x.IsDeleted)
                .OrderByDescending(x => x.NgayGui)
                .ToListAsync(cancellationToken);
        }

        public async Task<YeuCauSuCo?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            return await _context.YeuCauSuCos
                .Include(x => x.PhongTro)
                .Include(x => x.NguoiThue)
                .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);
        }

        public void Add(YeuCauSuCo model)
        {
            _context.YeuCauSuCos.Add(model);
        }

        public void Update(YeuCauSuCo model)
        {
            _context.YeuCauSuCos.Update(model);
        }
    }
}
