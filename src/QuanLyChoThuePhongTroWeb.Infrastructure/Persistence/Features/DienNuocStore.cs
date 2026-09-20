using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.Persistence;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Features
{
    public class DienNuocStore : IDienNuocStore
    {
        private readonly ApplicationDbContext _context;

        public DienNuocStore(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IReadOnlyList<HopDong>> GetActiveContractsInBranchAsync(int chiNhanhId, DateTime startOfMonth, DateTime endOfMonth, CancellationToken cancellationToken = default)
        {
            return await _context.HopDongs
                .Include(h => h.PhongTro)
                .Include(h => h.NguoiThue)
                .Where(h => h.PhongTro.ChiNhanhId == chiNhanhId &&
                            !h.IsDeleted &&
                            h.TrangThaiHopDong != TrangThaiHopDong.DaHuy &&
                            h.ThoiDiemBatDau <= endOfMonth &&
                            (h.ThoiDiemKetThuc == null || h.ThoiDiemKetThuc.Value >= startOfMonth))
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyDictionary<int, DichVuDienNuocCuaPhong>> GetCurrentMonthRecordsAsync(IReadOnlyList<int> roomIds, int thang, int nam, CancellationToken cancellationToken = default)
        {
            return await _context.DichVuDienNuocCuaPhongs
                .Where(x => roomIds.Contains(x.PhongTroId) && x.Thang == thang && x.Nam == nam && !x.IsDeleted)
                .ToDictionaryAsync(x => x.PhongTroId, cancellationToken);
        }

        public async Task<IReadOnlyDictionary<int, DichVuDienNuocCuaPhong>> GetPreviousMonthRecordsAsync(IReadOnlyList<int> roomIds, int prevThang, int prevNam, CancellationToken cancellationToken = default)
        {
            return await _context.DichVuDienNuocCuaPhongs
                .Where(x => roomIds.Contains(x.PhongTroId) && x.Thang == prevThang && x.Nam == prevNam && !x.IsDeleted)
                .ToDictionaryAsync(x => x.PhongTroId, cancellationToken);
        }

        public async Task<ISet<int>> GetLockedRoomIdsAsync(IReadOnlyList<int> roomIds, int thang, int nam, CancellationToken cancellationToken = default)
        {
            var list = await _context.DichVuDienNuocCuaPhongs
                .Where(x => roomIds.Contains(x.PhongTroId) && !x.IsDeleted &&
                            (x.Nam > nam || (x.Nam == nam && x.Thang > thang)))
                .Select(x => x.PhongTroId)
                .Distinct()
                .ToListAsync(cancellationToken);

            return new HashSet<int>(list);
        }

        public async Task<(double ChiSoDienMoi, double ChiSoNuocMoi)> GetNearestPreviousReadingAsync(int phongTroId, int thang, int nam, CancellationToken cancellationToken = default)
        {
            var record = await _context.DichVuDienNuocCuaPhongs
                .Where(x => x.PhongTroId == phongTroId && !x.IsDeleted &&
                            (x.Nam < nam || (x.Nam == nam && x.Thang < thang)))
                .OrderByDescending(x => x.Nam)
                .ThenByDescending(x => x.Thang)
                .FirstOrDefaultAsync(cancellationToken);

            return record != null ? (record.ChiSoDienMoi, record.ChiSoNuocMoi) : (0.0, 0.0);
        }

        public async Task<double> GetServicePriceAsync(string serviceKeyword, int chiNhanhId, CancellationToken cancellationToken = default)
        {
            var keyword = serviceKeyword.ToLower();
            var dichVu = await _context.DichVus.FirstOrDefaultAsync(x => x.TenDichVu.ToLower().Contains(keyword) && !x.IsDeleted, cancellationToken);
            if (dichVu == null) return 0;

            var bg = await _context.DichVuChiNhanhs.FirstOrDefaultAsync(x => x.DichVuId == dichVu.DichVuId && x.ChiNhanhId == chiNhanhId && !x.IsDeleted, cancellationToken);
            return bg?.GiaDichVu ?? 0;
        }

        public async Task<IReadOnlyDictionary<int, string>> GetRoomNumbersAsync(IReadOnlyList<int> roomIds, CancellationToken cancellationToken = default)
        {
            return await _context.PhongTros
                .Where(x => roomIds.Contains(x.PhongTroId))
                .ToDictionaryAsync(x => x.PhongTroId, x => x.SoPhong, cancellationToken);
        }

        public async Task AddRecordAsync(DichVuDienNuocCuaPhong record, CancellationToken cancellationToken = default)
        {
            await _context.DichVuDienNuocCuaPhongs.AddAsync(record, cancellationToken);
        }

        public void UpdateRecord(DichVuDienNuocCuaPhong record)
        {
            _context.DichVuDienNuocCuaPhongs.Update(record);
        }
    }
}
