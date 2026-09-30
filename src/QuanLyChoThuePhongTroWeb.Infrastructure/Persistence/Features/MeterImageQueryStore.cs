using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.Persistence;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Features
{
    public class MeterImageQueryStore : IMeterImageQueryStore
    {
        private readonly ApplicationDbContext _context;

        public MeterImageQueryStore(ApplicationDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<IReadOnlyList<MeterRoomRef>> GetTenantRoomsAsync(int tenantUserId, DateTime startUtc, DateTime endExclusiveUtc, CancellationToken ct = default)
        {
            var user = await _context.NguoiDungs
                .AsNoTracking()
                .Include(u => u.NguoiThue)
                .Where(u => u.NguoiDungId == tenantUserId &&
                            !u.IsDeleted &&
                            u.IsActive &&
                            u.Role == Role.KhachThue &&
                            u.NguoiThueId.HasValue &&
                            u.NguoiThue != null &&
                            !u.NguoiThue.IsDeleted)
                .Select(u => new { u.NguoiThueId })
                .FirstOrDefaultAsync(ct);

            if (user == null || !user.NguoiThueId.HasValue)
            {
                return Array.Empty<MeterRoomRef>();
            }

            var nguoiThueId = user.NguoiThueId.Value;

            var rooms = await _context.HopDongs
                .AsNoTracking()
                .Where(h => !h.IsDeleted &&
                            h.TrangThaiHopDong != TrangThaiHopDong.DaHuy &&
                            h.ThoiDiemBatDau < endExclusiveUtc &&
                            (h.ThoiDiemKetThuc == null || h.ThoiDiemKetThuc >= startUtc) &&
                            (h.NguoiThueId == nguoiThueId ||
                             h.ChiTietThanhVienHopDongs.Any(tv => !tv.IsDeleted && tv.NguoiThueId == nguoiThueId)) &&
                            !h.PhongTro.IsDeleted)
                .Select(h => new
                {
                    h.PhongTroId,
                    h.PhongTro.SoPhong,
                    h.PhongTro.ChiNhanhId
                })
                .Distinct()
                .OrderBy(r => r.SoPhong)
                .ToListAsync(ct);

            return rooms.Select(r => new MeterRoomRef(r.PhongTroId, r.SoPhong, r.ChiNhanhId)).ToList();
        }

        public async Task<MeterRoomRef?> GetRoomAsync(int phongTroId, CancellationToken ct = default)
        {
            return await _context.PhongTros
                .AsNoTracking()
                .Where(p => p.PhongTroId == phongTroId && !p.IsDeleted)
                .Select(p => new MeterRoomRef(p.PhongTroId, p.SoPhong, p.ChiNhanhId))
                .FirstOrDefaultAsync(ct);
        }

        public async Task<MeterPeriodSnapshot?> GetPeriodAsync(int phongTroId, int thang, int nam, CancellationToken ct = default)
        {
            return await _context.DichVuDienNuocCuaPhongs
                .AsNoTracking()
                .Where(p => p.PhongTroId == phongTroId && p.Thang == thang && p.Nam == nam && !p.IsDeleted)
                .Select(p => new MeterPeriodSnapshot(
                    p.DichVuDienNuocCuaPhongId,
                    p.TrangThaiGhiNhan,
                    p.ChiSoDienCu,
                    p.ChiSoDienMoi,
                    p.ChiSoNuocCu,
                    p.ChiSoNuocMoi))
                .FirstOrDefaultAsync(ct);
        }

        public async Task<IReadOnlyList<MeterImageSnapshot>> GetImagesAsync(int dichVuDienNuocCuaPhongId, CancellationToken ct = default)
        {
            return await _context.AnhChiSoDongHos
                .AsNoTracking()
                .Where(a => a.DichVuDienNuocCuaPhongId == dichVuDienNuocCuaPhongId && !a.IsDeleted)
                .OrderByDescending(a => a.NgayGui)
                .ThenByDescending(a => a.AnhChiSoDongHoId)
                .Select(a => new MeterImageSnapshot(
                    a.AnhChiSoDongHoId,
                    a.LoaiDongHo,
                    a.Url,
                    a.TrangThaiXuLy,
                    a.GiaTriAIGoiY,
                    a.DoTinCay,
                    a.GiaTriXacNhan,
                    a.DuocChonLamChiSoChinhThuc,
                    a.NgayGui,
                    a.NgayXuLy,
                    a.ThongBaoLoi,
                    a.GhiChuXacNhan,
                    a.NguoiGui != null && a.NguoiGui.Role == Role.KhachThue))
                .ToListAsync(ct);
        }
    }
}
