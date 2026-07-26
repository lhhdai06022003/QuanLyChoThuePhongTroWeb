using Microsoft.EntityFrameworkCore;
using QuanLyChoThuePhongTroWeb.Data;
using QuanLyChoThuePhongTroWeb.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Services.YeuCauSuCos
{
    public class YeuCauSuCoService : IYeuCauSuCoService
    {
        private readonly ApplicationDbContext _context;

        public YeuCauSuCoService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<YeuCauSuCo>> GetAllAsync(int? chiNhanhId, TrangThaiSuCo? trangThai, int? soThang = 6)
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

            return await query.OrderByDescending(x => x.NgayGui).ToListAsync();
        }

        public async Task<List<YeuCauSuCo>> GetByNguoiThueAsync(int nguoiThueId)
        {
            return await _context.YeuCauSuCos
                .Include(x => x.PhongTro)
                .Where(x => x.NguoiThueId == nguoiThueId && !x.IsDeleted)
                .OrderByDescending(x => x.NgayGui)
                .ToListAsync();
        }

        public async Task<YeuCauSuCo?> GetByIdAsync(int id)
        {
            return await _context.YeuCauSuCos
                .Include(x => x.PhongTro)
                .Include(x => x.NguoiThue)
                .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
        }

        public async Task<bool> CreateAsync(YeuCauSuCo model)
        {
            _context.YeuCauSuCos.Add(model);
            return await _context.SaveChangesAsync() > 0;
        }

        public async Task<bool> UpdateStatusAsync(int id, TrangThaiSuCo trangThai, double chiPhi, bool congVaoHoaDon, string? lyDoTuChoi, string? ghiChuAdmin)
        {
            var suco = await _context.YeuCauSuCos.FindAsync(id);
            if (suco == null || suco.IsDeleted) return false;

            suco.TrangThai = trangThai;
            suco.ChiPhiSuaChua = chiPhi;
            suco.CongVaoHoaDon = congVaoHoaDon;
            suco.GhiChuAdmin = ghiChuAdmin;
            
            if (trangThai == TrangThaiSuCo.DaHuy || trangThai == TrangThaiSuCo.DaHoanThanh)
            {
                suco.NgayXuLy = DateTime.UtcNow;
            }

            if (trangThai == TrangThaiSuCo.DaHuy)
            {
                suco.LyDoTuChoi = lyDoTuChoi;
            }
            else
            {
                suco.LyDoTuChoi = null; // Clear if not rejected
            }

            return await _context.SaveChangesAsync() > 0;
        }

        public async Task<bool> SoftDeleteAsync(int id)
        {
            var suco = await _context.YeuCauSuCos.FindAsync(id);
            if (suco == null) return false;

            suco.IsDeleted = true;
            return await _context.SaveChangesAsync() > 0;
        }
    }
}
