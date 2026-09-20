using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Features.ThongBaos.Persistence;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Features
{
    public class ThongBaoStore : IThongBaoStore
    {
        private readonly ApplicationDbContext _context;

        public ThongBaoStore(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<ThongBao>> LayDanhSachTheoNguoiDungAsync(int nguoiDungId, CancellationToken cancellationToken = default)
        {
            return await _context.ThongBaos
                .AsNoTracking()
                .Where(x => x.NguoiDungId == nguoiDungId)
                .OrderByDescending(x => x.CreatedAt)
                .Take(20)
                .ToListAsync(cancellationToken);
        }

        public async Task<NguoiDung?> GetActiveNguoiDungByNguoiThueIdAsync(int nguoiThueId, CancellationToken cancellationToken = default)
        {
            return await _context.NguoiDungs
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.NguoiThueId == nguoiThueId && !u.IsDeleted && u.IsActive, cancellationToken);
        }

        public async Task<List<int>> GetActiveUserIdsByRoleAsync(Role role, CancellationToken cancellationToken = default)
        {
            return await _context.NguoiDungs
                .AsNoTracking()
                .Where(u => u.Role == role && !u.IsDeleted && u.IsActive)
                .Select(u => u.NguoiDungId)
                .ToListAsync(cancellationToken);
        }

        public async Task<ThongBao?> GetByIdAsync(int thongBaoId, CancellationToken cancellationToken = default)
        {
            return await _context.ThongBaos.FirstOrDefaultAsync(x => x.Id == thongBaoId, cancellationToken);
        }

        public async Task<ThongBao?> GetByIdAndUserAsync(int thongBaoId, int nguoiDungId, CancellationToken cancellationToken = default)
        {
            return await _context.ThongBaos.FirstOrDefaultAsync(x => x.Id == thongBaoId && x.NguoiDungId == nguoiDungId, cancellationToken);
        }

        public async Task<int> LaySoLuongChuaDocAsync(int nguoiDungId, CancellationToken cancellationToken = default)
        {
            return await _context.ThongBaos
                .AsNoTracking()
                .CountAsync(x => x.NguoiDungId == nguoiDungId && !x.IsRead, cancellationToken);
        }

        public async Task<List<ThongBao>> GetUnreadByUserAsync(int nguoiDungId, CancellationToken cancellationToken = default)
        {
            return await _context.ThongBaos
                .Where(x => x.NguoiDungId == nguoiDungId && !x.IsRead)
                .ToListAsync(cancellationToken);
        }

        public async Task<List<ThongBao>> GetAllByUserAsync(int nguoiDungId, CancellationToken cancellationToken = default)
        {
            return await _context.ThongBaos
                .Where(x => x.NguoiDungId == nguoiDungId)
                .ToListAsync(cancellationToken);
        }

        public async Task<DataTableResponse<ThongBao>> LayDanhSachPhanTrangAsync(DataTableRequest request, int nguoiDungId, bool? chuaDoc, CancellationToken cancellationToken = default)
        {
            int recordsTotal = await _context.ThongBaos
                .AsNoTracking()
                .CountAsync(x => x.NguoiDungId == nguoiDungId, cancellationToken);

            var query = _context.ThongBaos
                .AsNoTracking()
                .Where(x => x.NguoiDungId == nguoiDungId);

            if (chuaDoc.HasValue)
            {
                query = query.Where(x => x.IsRead == !chuaDoc.Value);
            }

            if (!string.IsNullOrEmpty(request.SearchValue))
            {
                string searchLower = request.SearchValue.ToLower();
                query = query.Where(x => x.TieuDe.ToLower().Contains(searchLower)
                                      || x.NoiDung.ToLower().Contains(searchLower));
            }

            int recordsFiltered = await query.CountAsync(cancellationToken);

            var data = await query
                .OrderByDescending(x => x.CreatedAt)
                .Skip(request.Start)
                .Take(request.Length)
                .ToListAsync(cancellationToken);

            return new DataTableResponse<ThongBao>
            {
                draw = request.Draw,
                recordsTotal = recordsTotal,
                recordsFiltered = recordsFiltered,
                data = data
            };
        }

        public void Add(ThongBao thongBao)
        {
            _context.ThongBaos.Add(thongBao);
        }

        public void AddRange(IEnumerable<ThongBao> thongBaos)
        {
            _context.ThongBaos.AddRange(thongBaos);
        }

        public void Remove(ThongBao thongBao)
        {
            _context.ThongBaos.Remove(thongBao);
        }

        public void RemoveRange(IEnumerable<ThongBao> thongBaos)
        {
            _context.ThongBaos.RemoveRange(thongBaos);
        }
    }
}
