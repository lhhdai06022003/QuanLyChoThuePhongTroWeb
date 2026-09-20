using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using QuanLyChoThuePhongTroWeb.Application.Features.DieuKhoanMaus.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.DieuKhoanMaus.Persistence;
using QuanLyChoThuePhongTroWeb.Domain.Entities;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Features
{
    public class DieuKhoanMauStore : IDieuKhoanMauStore
    {
        private readonly ApplicationDbContext _context;

        public DieuKhoanMauStore(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<DieuKhoanMauRes>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return await _context.DieuKhoanMaus
                .Where(x => !x.IsDeleted)
                .OrderByDescending(x => x.NgayTao)
                .Select(x => new DieuKhoanMauRes
                {
                    DieuKhoanMauId = x.DieuKhoanMauId,
                    TieuDe = x.TieuDe,
                    NoiDung = x.NoiDung,
                    NgayTao = x.NgayTao,
                    NgayCapNhat = x.NgayCapNhat
                })
                .ToListAsync(cancellationToken);
        }

        public async Task<DieuKhoanMau?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            return await _context.DieuKhoanMaus
                .FirstOrDefaultAsync(x => x.DieuKhoanMauId == id && !x.IsDeleted, cancellationToken);
        }

        public void Add(DieuKhoanMau entity)
        {
            _context.DieuKhoanMaus.Add(entity);
        }

        public void Update(DieuKhoanMau entity)
        {
            _context.DieuKhoanMaus.Update(entity);
        }
    }
}
