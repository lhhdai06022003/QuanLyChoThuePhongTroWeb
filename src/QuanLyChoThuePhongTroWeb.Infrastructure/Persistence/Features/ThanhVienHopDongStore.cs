using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using QuanLyChoThuePhongTroWeb.Application.Features.ThanhVienHopDongs.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.ThanhVienHopDongs.Persistence;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Features
{
    public class ThanhVienHopDongStore : IThanhVienHopDongStore
    {
        private readonly ApplicationDbContext _context;

        public ThanhVienHopDongStore(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<HopDong?> GetHopDongWithPhongTroAsync(int hopDongId, CancellationToken cancellationToken = default)
        {
            return await _context.HopDongs
                .Include(x => x.PhongTro)
                .FirstOrDefaultAsync(x => x.HopDongId == hopDongId && !x.IsDeleted, cancellationToken);
        }

        public async Task<List<ThanhVienHopDongRes>> GetThanhVienByHopDongIdAsync(int hopDongId, CancellationToken cancellationToken = default)
        {
            var hopDong = await _context.HopDongs.FirstOrDefaultAsync(x => x.HopDongId == hopDongId, cancellationToken);
            if (hopDong == null) return new List<ThanhVienHopDongRes>();

            return await _context.ChiTietThanhVienHopDongs
                .Include(x => x.NguoiThue)
                .Where(x => x.HopDongId == hopDongId && !x.IsDeleted)
                .Select(x => new ThanhVienHopDongRes
                {
                    ChiTietThanhVienHopDongId = x.ChiTietThanhVienHopDongId,
                    NguoiThueId = x.NguoiThueId,
                    HoVaTen = x.NguoiThue.HoVaTen,
                    SoDienThoai = x.NguoiThue.SoDienThoai,
                    CCCD = x.NguoiThue.CCCD,
                    NgayVao = x.NgayVao,
                    NgayChuyenDi = x.NgayChuyenDi,
                    IsChuHopDong = x.NguoiThueId == hopDong.NguoiThueId
                })
                .OrderByDescending(x => x.IsChuHopDong)
                .ThenBy(x => x.NgayVao)
                .ToListAsync(cancellationToken);
        }

        public async Task<int> CountActiveMembersAsync(int hopDongId, CancellationToken cancellationToken = default)
        {
            return await _context.ChiTietThanhVienHopDongs
                .CountAsync(x => x.HopDongId == hopDongId && !x.IsDeleted && x.NgayChuyenDi == null, cancellationToken);
        }

        public async Task<bool> ExistsNguoiThueAsync(int nguoiThueId, CancellationToken cancellationToken = default)
        {
            return await _context.NguoiThues.AnyAsync(x => x.NguoiThueId == nguoiThueId && !x.IsDeleted, cancellationToken);
        }

        public async Task<NguoiThue?> GetNguoiThueByCccdAsync(string cccd, CancellationToken cancellationToken = default)
        {
            return await _context.NguoiThues.FirstOrDefaultAsync(x => x.CCCD == cccd && !x.IsDeleted, cancellationToken);
        }

        public async Task<bool> IsNguoiThueOverlappingAsync(int nguoiThueId, CancellationToken cancellationToken = default)
        {
            return await _context.ChiTietThanhVienHopDongs
                .Include(x => x.HopDong)
                .AnyAsync(x => x.NguoiThueId == nguoiThueId &&
                               x.NgayChuyenDi == null &&
                               !x.IsDeleted &&
                               x.HopDong.TrangThaiHopDong == TrangThaiHopDong.DangHoatDong,
                               cancellationToken);
        }

        public async Task<ChiTietThanhVienHopDong?> GetChiTietWithHopDongAsync(int chiTietId, CancellationToken cancellationToken = default)
        {
            return await _context.ChiTietThanhVienHopDongs
                .Include(x => x.HopDong)
                .FirstOrDefaultAsync(x => x.ChiTietThanhVienHopDongId == chiTietId && !x.IsDeleted, cancellationToken);
        }

        public void AddNguoiThue(NguoiThue nguoiThue)
        {
            _context.NguoiThues.Add(nguoiThue);
        }

        public void AddChiTiet(ChiTietThanhVienHopDong chiTiet)
        {
            _context.ChiTietThanhVienHopDongs.Add(chiTiet);
        }
    }
}
