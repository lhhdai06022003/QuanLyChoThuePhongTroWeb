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
    public class NguoiThueStore : INguoiThueStore
    {
        private readonly ApplicationDbContext _context;

        public NguoiThueStore(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<NguoiThue>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return await _context.NguoiThues
                .Where(x => !x.IsDeleted)
                .OrderByDescending(x => x.NgayTao)
                .ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<NguoiThue>> GetAvailableAsync(CancellationToken cancellationToken = default)
        {
            return await _context.NguoiThues
                .Where(x => !x.IsDeleted)
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

        public async Task<IReadOnlyList<SelectOptionDto>> GetDropdownListAsync(CancellationToken cancellationToken = default)
        {
            return await _context.NguoiThues
                .Where(p => !p.IsDeleted)
                .Select(p => new SelectOptionDto(p.NguoiThueId.ToString(), p.HoVaTen + " - " + p.CCCD))
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<SelectOptionDto>> GetDropdownChuaCoPhongAsync(CancellationToken cancellationToken = default)
        {
            return await _context.NguoiThues
                .Where(x => !x.IsDeleted)
                .Where(x => !x.HopDongs.Any(h => h.TrangThaiHopDong == TrangThaiHopDong.DangHoatDong))
                .Where(x => !x.ChiTietThanhVienHopDongs.Any(tv => tv.NgayChuyenDi == null && tv.HopDong.TrangThaiHopDong == TrangThaiHopDong.DangHoatDong))
                .Select(p => new SelectOptionDto(p.NguoiThueId.ToString(), p.HoVaTen + " - " + p.CCCD))
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<SelectOptionDto>> GetDropdownCoHopDongAsync(CancellationToken cancellationToken = default)
        {
            return await _context.NguoiThues
                .Where(x => !x.IsDeleted)
                .Where(x => x.HopDongs.Any())
                .Select(p => new SelectOptionDto(p.NguoiThueId.ToString(), p.HoVaTen + " - " + p.CCCD))
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<NguoiThueAutocompleteDto>> SearchAutocompleteAsync(string searchTerm, CancellationToken cancellationToken = default)
        {
            searchTerm = (searchTerm ?? "").Trim().ToLower();
            return await _context.NguoiThues
                .Where(x => !x.IsDeleted)
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
