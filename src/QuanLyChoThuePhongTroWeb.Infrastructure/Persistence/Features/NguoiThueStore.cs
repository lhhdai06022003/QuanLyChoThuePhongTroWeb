using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Features.NguoiThues.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.NguoiThues.Persistence;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Features
{
    public class NguoiThueStore : INguoiThueStore, ITenantVisibilityStore
    {
        private readonly ApplicationDbContext _context;

        public NguoiThueStore(ApplicationDbContext context)
        {
            _context = context;
        }

        // Quy tắc nhìn thấy người thuê theo chi nhánh, dùng chung cho mọi truy vấn để không lệch nhau:
        // chưa có hợp đồng nào (đứng tên hay ở ghép), hoặc có hợp đồng (kể cả đã kết thúc) thuộc chi nhánh được phép.
        private static IQueryable<NguoiThue> ApplyVisibility(IQueryable<NguoiThue> query, IReadOnlyCollection<int>? allowedBranchIds)
        {
            if (allowedBranchIds == null)
            {
                return query;
            }

            return query.Where(x =>
                (!x.HopDongs.Any(h => !h.IsDeleted) &&
                 !x.ChiTietThanhVienHopDongs.Any(tv => !tv.IsDeleted && !tv.HopDong.IsDeleted))
                || x.HopDongs.Any(h => !h.IsDeleted && allowedBranchIds.Contains(h.PhongTro.ChiNhanhId))
                || x.ChiTietThanhVienHopDongs.Any(tv => !tv.IsDeleted && !tv.HopDong.IsDeleted && allowedBranchIds.Contains(tv.HopDong.PhongTro.ChiNhanhId)));
        }

        public async Task<bool> AreAllVisibleAsync(IReadOnlyCollection<int> nguoiThueIds, IReadOnlyCollection<int> allowedBranchIds, CancellationToken cancellationToken = default)
        {
            var ids = nguoiThueIds.Distinct().ToList();
            if (ids.Count == 0) return true;

            var visible = await ApplyVisibility(_context.NguoiThues.Where(x => ids.Contains(x.NguoiThueId) && !x.IsDeleted), allowedBranchIds)
                .CountAsync(cancellationToken);
            return visible == ids.Count;
        }

        public async Task<bool> IsVisibleAsync(int nguoiThueId, IReadOnlyCollection<int> allowedBranchIds, CancellationToken cancellationToken = default)
        {
            return await ApplyVisibility(_context.NguoiThues.Where(x => x.NguoiThueId == nguoiThueId && !x.IsDeleted), allowedBranchIds)
                .AnyAsync(cancellationToken);
        }

        public async Task<IEnumerable<NguoiThue>> GetAllAsync(IReadOnlyCollection<int>? allowedBranchIds = null, CancellationToken cancellationToken = default)
        {
            return await ApplyVisibility(_context.NguoiThues.Where(x => !x.IsDeleted), allowedBranchIds)
                .OrderByDescending(x => x.NgayTao)
                .ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<NguoiThue>> GetAvailableAsync(IReadOnlyCollection<int>? allowedBranchIds = null, CancellationToken cancellationToken = default)
        {
            return await ApplyVisibility(_context.NguoiThues.Where(x => !x.IsDeleted), allowedBranchIds)
                .Where(x => !x.HopDongs.Any(h => h.TrangThaiHopDong == TrangThaiHopDong.DangHoatDong))
                .Where(x => !x.ChiTietThanhVienHopDongs.Any(tv => tv.NgayChuyenDi == null && tv.HopDong.TrangThaiHopDong == TrangThaiHopDong.DangHoatDong))
                .OrderByDescending(x => x.NgayTao)
                .ToListAsync(cancellationToken);
        }

        public async Task<NguoiThue?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            return await _context.NguoiThues
                .FirstOrDefaultAsync(x => x.NguoiThueId == id && !x.IsDeleted, cancellationToken);
        }

        public async Task<bool> ExistsDuplicateAsync(string cccd, string soDienThoai, string email, int excludeId = 0, CancellationToken cancellationToken = default)
        {
            var query = _context.NguoiThues.Where(x => !x.IsDeleted);
            if (excludeId > 0)
            {
                query = query.Where(x => x.NguoiThueId != excludeId);
            }

            return await query.AnyAsync(x =>
                x.CCCD == cccd ||
                x.SoDienThoai == soDienThoai ||
                x.Email == email,
                cancellationToken);
        }

        public async Task<bool> IsDaiDienHopDongHoatDongAsync(int nguoiThueId, CancellationToken cancellationToken = default)
        {
            return await _context.HopDongs
                .AnyAsync(h => h.NguoiThueId == nguoiThueId && h.TrangThaiHopDong == TrangThaiHopDong.DangHoatDong && !h.IsDeleted, cancellationToken);
        }

        public async Task<bool> IsThanhVienHopDongHoatDongAsync(int nguoiThueId, CancellationToken cancellationToken = default)
        {
            return await _context.ChiTietThanhVienHopDongs
                .AnyAsync(tv => tv.NguoiThueId == nguoiThueId && tv.NgayChuyenDi == null && !tv.IsDeleted && tv.HopDong.TrangThaiHopDong == TrangThaiHopDong.DangHoatDong, cancellationToken);
        }

        public async Task<IReadOnlyList<SelectOptionDto>> GetDropdownListAsync(IReadOnlyCollection<int>? allowedBranchIds = null, CancellationToken cancellationToken = default)
        {
            return await ApplyVisibility(_context.NguoiThues.Where(p => !p.IsDeleted), allowedBranchIds)
                .Select(p => new SelectOptionDto(p.NguoiThueId.ToString(), p.HoVaTen + " - " + p.CCCD))
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<SelectOptionDto>> GetDropdownChuaCoPhongAsync(IReadOnlyCollection<int>? allowedBranchIds = null, CancellationToken cancellationToken = default)
        {
            return await ApplyVisibility(_context.NguoiThues.Where(x => !x.IsDeleted), allowedBranchIds)
                .Where(x => !x.HopDongs.Any(h => h.TrangThaiHopDong == TrangThaiHopDong.DangHoatDong))
                .Where(x => !x.ChiTietThanhVienHopDongs.Any(tv => tv.NgayChuyenDi == null && tv.HopDong.TrangThaiHopDong == TrangThaiHopDong.DangHoatDong))
                .Select(p => new SelectOptionDto(p.NguoiThueId.ToString(), p.HoVaTen + " - " + p.CCCD))
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<SelectOptionDto>> GetDropdownCoHopDongAsync(IReadOnlyCollection<int>? allowedBranchIds = null, CancellationToken cancellationToken = default)
        {
            return await ApplyVisibility(_context.NguoiThues.Where(x => !x.IsDeleted), allowedBranchIds)
                .Where(x => x.HopDongs.Any())
                .Select(p => new SelectOptionDto(p.NguoiThueId.ToString(), p.HoVaTen + " - " + p.CCCD))
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<NguoiThueAutocompleteDto>> SearchAutocompleteAsync(string searchTerm, IReadOnlyCollection<int>? allowedBranchIds = null, CancellationToken cancellationToken = default)
        {
            searchTerm = (searchTerm ?? "").Trim().ToLower();
            return await ApplyVisibility(_context.NguoiThues.Where(x => !x.IsDeleted), allowedBranchIds)
                .Where(x => !x.HopDongs.Any(h => h.TrangThaiHopDong == TrangThaiHopDong.DangHoatDong && !h.IsDeleted))
                .Where(x => !x.ChiTietThanhVienHopDongs.Any(tv => tv.NgayChuyenDi == null && !tv.IsDeleted && tv.HopDong.TrangThaiHopDong == TrangThaiHopDong.DangHoatDong))
                .Where(x =>
                    string.IsNullOrEmpty(searchTerm) ||
                    x.HoVaTen.ToLower().Contains(searchTerm) ||
                    x.SoDienThoai.Contains(searchTerm) ||
                    x.CCCD.Contains(searchTerm)
                )
                .Select(x => new NguoiThueAutocompleteDto
                {
                    Id = x.NguoiThueId,
                    HoVaTen = x.HoVaTen,
                    SoDienThoai = x.SoDienThoai,
                    CCCD = x.CCCD
                })
                .Take(10)
                .ToListAsync(cancellationToken);
        }

        public void Add(NguoiThue entity)
        {
            _context.NguoiThues.Add(entity);
        }
    }
}
