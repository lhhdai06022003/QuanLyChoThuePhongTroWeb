using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Features.DichVus.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.DichVus.Persistence;
using QuanLyChoThuePhongTroWeb.Domain.Entities;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Features
{
    public class DichVuStore : IDichVuStore
    {
        private readonly ApplicationDbContext _context;

        public DichVuStore(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<DataTableResponse<DichVuRes>> GetPagedDichVuAsync(DataTableRequest request, CancellationToken cancellationToken = default)
        {
            var query = _context.DichVus.Where(x => !x.IsDeleted);

            if (!string.IsNullOrEmpty(request.SearchValue))
            {
                string searchLower = request.SearchValue.ToLower();
                query = query.Where(x => x.TenDichVu.ToLower().Contains(searchLower));
            }

            int totalRecords = await query.CountAsync(cancellationToken);
            int filteredRecords = totalRecords;

            if (!string.IsNullOrEmpty(request.SortColumnName))
            {
                if (request.SortColumnName == "tenDichVu")
                    query = request.SortDirection == "asc" ? query.OrderBy(x => x.TenDichVu) : query.OrderByDescending(x => x.TenDichVu);
                else
                    query = query.OrderByDescending(x => x.DichVuId);
            }
            else
            {
                query = query.OrderByDescending(x => x.DichVuId);
            }

            var data = await query
                .Select(x => new DichVuRes
                {
                    DichVuId = x.DichVuId,
                    TenDichVu = x.TenDichVu,
                    DonVi = x.DonVi,
                    GhiChu = x.GhiChu
                })
                .Skip(request.Start)
                .Take(request.Length)
                .ToListAsync(cancellationToken);

            return new DataTableResponse<DichVuRes>
            {
                draw = request.Draw,
                recordsTotal = totalRecords,
                recordsFiltered = filteredRecords,
                data = data
            };
        }

        public async Task<DichVu?> GetDichVuByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            return await _context.DichVus.FirstOrDefaultAsync(x => x.DichVuId == id && !x.IsDeleted, cancellationToken);
        }

        public async Task<bool> ExistsDichVuNameAsync(string name, int excludeId = 0, CancellationToken cancellationToken = default)
        {
            string lowerName = name.ToLower();
            return await _context.DichVus
                .AnyAsync(x => x.DichVuId != excludeId && x.TenDichVu.ToLower() == lowerName && !x.IsDeleted, cancellationToken);
        }

        public async Task<bool> IsDichVuInUseAsync(int dichVuId, CancellationToken cancellationToken = default)
        {
            return await _context.DichVuChiNhanhs.AnyAsync(x => x.DichVuId == dichVuId && !x.IsDeleted, cancellationToken);
        }

        public void AddDichVu(DichVu entity)
        {
            _context.DichVus.Add(entity);
        }

        public void UpdateDichVu(DichVu entity)
        {
            _context.DichVus.Update(entity);
        }

        public async Task<DataTableResponse<DichVuChiNhanhRes>> GetPagedDichVuChiNhanhAsync(DataTableRequest request, int chiNhanhId, CancellationToken cancellationToken = default)
        {
            var query = _context.DichVuChiNhanhs
                .Include(x => x.DichVu)
                .Include(x => x.ChiNhanh)
                .Where(x => !x.IsDeleted);

            if (chiNhanhId > 0)
            {
                query = query.Where(x => x.ChiNhanhId == chiNhanhId);
            }

            if (!string.IsNullOrEmpty(request.SearchValue))
            {
                string searchLower = request.SearchValue.ToLower();
                query = query.Where(x => x.DichVu.TenDichVu.ToLower().Contains(searchLower) || x.ChiNhanh.TenChiNhanh.ToLower().Contains(searchLower));
            }

            int totalRecords = await query.CountAsync(cancellationToken);
            int filteredRecords = totalRecords;

            query = query.OrderByDescending(x => x.DichVuChiNhanhId);

            var data = await query
                .Select(x => new DichVuChiNhanhRes
                {
                    DichVuChiNhanhId = x.DichVuChiNhanhId,
                    ChiNhanhId = x.ChiNhanhId,
                    TenChiNhanh = x.ChiNhanh.TenChiNhanh,
                    DichVuId = x.DichVuId,
                    TenDichVu = x.DichVu.TenDichVu,
                    GiaDichVu = x.GiaDichVu,
                    MacDinh = x.MacDinh
                })
                .Skip(request.Start)
                .Take(request.Length)
                .ToListAsync(cancellationToken);

            return new DataTableResponse<DichVuChiNhanhRes>
            {
                draw = request.Draw,
                recordsTotal = totalRecords,
                recordsFiltered = filteredRecords,
                data = data
            };
        }

        public async Task<DichVuChiNhanh?> GetDichVuChiNhanhByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            return await _context.DichVuChiNhanhs
                .Include(x => x.DichVu)
                .Include(x => x.ChiNhanh)
                .FirstOrDefaultAsync(x => x.DichVuChiNhanhId == id && !x.IsDeleted, cancellationToken);
        }

        public async Task<bool> ExistsDichVuChiNhanhAsync(int chiNhanhId, int dichVuId, int excludeId = 0, CancellationToken cancellationToken = default)
        {
            return await _context.DichVuChiNhanhs
                .AnyAsync(x => x.DichVuChiNhanhId != excludeId && x.ChiNhanhId == chiNhanhId && x.DichVuId == dichVuId && !x.IsDeleted, cancellationToken);
        }

        public void AddDichVuChiNhanh(DichVuChiNhanh entity)
        {
            _context.DichVuChiNhanhs.Add(entity);
        }

        public void UpdateDichVuChiNhanh(DichVuChiNhanh entity)
        {
            _context.DichVuChiNhanhs.Update(entity);
        }

        public async Task<PhongTro?> GetPhongTroByIdAsync(int phongTroId, CancellationToken cancellationToken = default)
        {
            return await _context.PhongTros.FirstOrDefaultAsync(p => p.PhongTroId == phongTroId && !p.IsDeleted, cancellationToken);
        }

        public async Task<List<DichVuChiNhanh>> GetActiveDichVuChiNhanhsAsync(int chiNhanhId, CancellationToken cancellationToken = default)
        {
            return await _context.DichVuChiNhanhs
                .Include(x => x.DichVu)
                .Where(x => x.ChiNhanhId == chiNhanhId && !x.IsDeleted && !x.DichVu.IsDeleted)
                .ToListAsync(cancellationToken);
        }

        public async Task<List<DangKyDichVu>> GetActiveDangKyDichVusAsync(int phongTroId, CancellationToken cancellationToken = default)
        {
            return await _context.DangKyDichVus
                .Where(x => x.PhongTroId == phongTroId && x.NgayKetThuc == null)
                .ToListAsync(cancellationToken);
        }

        public void AddDangKyDichVu(DangKyDichVu entity)
        {
            _context.DangKyDichVus.Add(entity);
        }
    }
}
