using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using QuanLyChoThuePhongTroWeb.Application.Features.HopDongs.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Features.HopDongs.Services;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Features
{
    public class HopDongLifecycleStore : IHopDongLifecycleStore
    {
        private readonly ApplicationDbContext _context;

        public HopDongLifecycleStore(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<HopDong?> GetHopDongAsync(int hopDongId, CancellationToken ct = default)
        {
            return await _context.HopDongs
                .AsNoTracking()
                .Include(x => x.PhongTro).ThenInclude(p => p.ChiNhanh)
                .Include(x => x.NguoiThue)
                .FirstOrDefaultAsync(x => x.HopDongId == hopDongId && !x.IsDeleted, ct);
        }

        public async Task<HopDong?> LockHopDongAsync(int hopDongId, CancellationToken ct = default)
        {
            return await _context.HopDongs
                .FromSqlInterpolated($"SELECT * FROM hop_dong WHERE \"HopDongId\" = {hopDongId} AND \"IsDeleted\" = false FOR UPDATE")
                .SingleOrDefaultAsync(ct);
        }

        public async Task<IReadOnlyList<HoaDon>> LockInvoicesOfContractsAsync(IReadOnlyCollection<int> hopDongIds, CancellationToken ct = default)
        {
            var ids = hopDongIds.Distinct().OrderBy(x => x).ToArray();
            if (ids.Length == 0)
            {
                return Array.Empty<HoaDon>();
            }

            return await _context.HoaDons
                .FromSqlInterpolated($"SELECT * FROM hoa_don WHERE \"HopDongId\" = ANY({ids}) AND \"IsDeleted\" = false ORDER BY \"HoaDonId\" FOR UPDATE")
                .ToListAsync(ct);
        }

        public async Task<IReadOnlyList<HoaDon>> GetInvoicesOfContractsAsync(IReadOnlyCollection<int> hopDongIds, CancellationToken ct = default)
        {
            var ids = hopDongIds.Distinct().ToList();
            return await _context.HoaDons
                .AsNoTracking()
                .Where(h => ids.Contains(h.HopDongId) && !h.IsDeleted)
                .OrderBy(h => h.HoaDonId)
                .ToListAsync(ct);
        }

        public async Task<IReadOnlyDictionary<int, decimal>> GetPaidTotalsAsync(IReadOnlyCollection<int> hoaDonIds, CancellationToken ct = default)
        {
            var ids = hoaDonIds.Distinct().ToList();
            return await _context.LichSuThanhToans
                .AsNoTracking()
                .Where(l => ids.Contains(l.HoaDonId) && !l.IsDeleted)
                .GroupBy(l => l.HoaDonId)
                .Select(g => new { g.Key, Tong = g.Sum(x => x.SoTienThanhToan) })
                .ToDictionaryAsync(x => x.Key, x => x.Tong, ct);
        }

        public async Task<IReadOnlyDictionary<int, decimal>> GetOffsetTotalsAsync(IReadOnlyCollection<int> hoaDonIds, CancellationToken ct = default)
        {
            var ids = hoaDonIds.Distinct().ToList();
            return await _context.LichSuThanhToans
                .AsNoTracking()
                .Where(l => ids.Contains(l.HoaDonId) && !l.IsDeleted && l.PhuongThucThanhToan == PhuongThucThanhToan.CanTruTienCoc)
                .GroupBy(l => l.HoaDonId)
                .Select(g => new { g.Key, Tong = g.Sum(x => x.SoTienThanhToan) })
                .ToDictionaryAsync(x => x.Key, x => x.Tong, ct);
        }

        public async Task<IReadOnlyList<YeuCauThanhToanHoaDon>> GetActivePaymentRequestsAsync(IReadOnlyCollection<int> hoaDonIds, DateTime nowUtc, CancellationToken ct = default)
        {
            var ids = hoaDonIds.Distinct().ToList();
            return await _context.YeuCauThanhToanHoaDons
                .Where(y => ids.Contains(y.HoaDonId))
                .Where(YeuCauThanhToanHoaDon.DangHoatDong(nowUtc))
                .ToListAsync(ct);
        }

        public void AddLedgerEntry(LichSuThanhToan entry)
        {
            _context.LichSuThanhToans.Add(entry);
        }

        public async Task<bool> HasInvoiceFromPeriodAsync(int hopDongId, int thang, int nam, CancellationToken ct = default)
        {
            return await _context.HoaDons.AnyAsync(h => h.HopDongId == hopDongId && !h.IsDeleted
                && (h.Nam > nam || (h.Nam == nam && h.Thang >= thang)), ct);
        }

        public async Task<PhongTro?> GetPhongTroForUpdateAsync(int phongTroId, CancellationToken ct = default)
        {
            return await _context.PhongTros.FirstOrDefaultAsync(p => p.PhongTroId == phongTroId, ct);
        }

        public async Task<bool> IsRoomRentedExcludingContractAsync(int phongTroId, int excludeHopDongId, CancellationToken ct = default)
        {
            return await _context.HopDongs.AnyAsync(x =>
                x.PhongTroId == phongTroId &&
                x.HopDongId != excludeHopDongId &&
                x.TrangThaiHopDong == TrangThaiHopDong.DangHoatDong &&
                !x.IsDeleted, ct);
        }

        public async Task<bool> IsTenantActiveInAnotherContractAsync(int nguoiThueId, int excludeHopDongId, CancellationToken ct = default)
        {
            return await _context.HopDongs.AnyAsync(x =>
                x.NguoiThueId == nguoiThueId &&
                x.HopDongId != excludeHopDongId &&
                x.TrangThaiHopDong == TrangThaiHopDong.DangHoatDong &&
                !x.IsDeleted, ct);
        }

        public async Task<IReadOnlyList<ChiTietThanhVienHopDong>> GetActiveMembersAsync(int hopDongId, CancellationToken ct = default)
        {
            return await _context.ChiTietThanhVienHopDongs
                .Where(x => x.HopDongId == hopDongId && x.NgayChuyenDi == null && !x.IsDeleted)
                .ToListAsync(ct);
        }

        public async Task<IReadOnlyList<ChiTietThanhVienHopDong>> GetMembersAsync(int hopDongId, CancellationToken ct = default)
        {
            return await _context.ChiTietThanhVienHopDongs
                .AsNoTracking()
                .Include(x => x.NguoiThue)
                .Where(x => x.HopDongId == hopDongId && !x.IsDeleted)
                .OrderBy(x => x.ChiTietThanhVienHopDongId)
                .ToListAsync(ct);
        }

        public async Task<IReadOnlyList<DangKyDichVu>> GetActiveServicesOfRoomAsync(int phongTroId, CancellationToken ct = default)
        {
            return await _context.DangKyDichVus
                .Where(x => x.PhongTroId == phongTroId && x.NgayKetThuc == null)
                .ToListAsync(ct);
        }

        public async Task<IReadOnlyList<HopDongDieuKhoan>> GetTermsAsync(int hopDongId, CancellationToken ct = default)
        {
            return await _context.HopDongDieuKhoans
                .AsNoTracking()
                .Where(x => x.HopDongId == hopDongId)
                .OrderBy(x => x.ThuTu).ThenBy(x => x.Id)
                .ToListAsync(ct);
        }

        public async Task<IReadOnlyList<DieuKhoanMau>> GetActiveTermTemplatesByIdsAsync(IReadOnlyList<int> ids, CancellationToken ct = default)
        {
            return await _context.DieuKhoanMaus
                .Where(x => ids.Contains(x.DieuKhoanMauId) && !x.IsDeleted)
                .ToListAsync(ct);
        }

        public async Task<int> CountActiveRenewalsAsync(string maGoc, CancellationToken ct = default)
        {
            var codes = await _context.HopDongs
                .Where(x => !x.IsDeleted && x.MaHopDong.StartsWith(maGoc + "-GH"))
                .Select(x => x.MaHopDong)
                .ToListAsync(ct);
            return codes.Count(c => HopDongCodeChain.IsRenewal(c) && HopDongCodeChain.BelongsTo(c, maGoc));
        }

        public async Task<bool> ExistsContractWithCodeAsync(string code, CancellationToken ct = default)
        {
            return await _context.HopDongs.AnyAsync(x => x.MaHopDong == code, ct);
        }

        public async Task<IReadOnlyList<HopDong>> GetChainAsync(string maGoc, CancellationToken ct = default)
        {
            var prefix = maGoc + "-GH";
            return await _context.HopDongs
                .AsNoTracking()
                .Where(x => !x.IsDeleted && (x.MaHopDong == maGoc || x.MaHopDong.StartsWith(prefix)))
                .ToListAsync(ct);
        }

        public async Task AddHopDongAsync(HopDong hopDong, CancellationToken ct = default)
        {
            await _context.HopDongs.AddAsync(hopDong, ct);
        }

        public async Task AddMembersAsync(IEnumerable<ChiTietThanhVienHopDong> members, CancellationToken ct = default)
        {
            await _context.ChiTietThanhVienHopDongs.AddRangeAsync(members, ct);
        }

        public async Task AddTermsAsync(IEnumerable<HopDongDieuKhoan> terms, CancellationToken ct = default)
        {
            await _context.HopDongDieuKhoans.AddRangeAsync(terms, ct);
        }

        public async Task<IReadOnlyList<LichSuThueRow>> GetRentalHistoryAsync(int nguoiThueId, IReadOnlyCollection<int>? allowedBranchIds, CancellationToken ct = default)
        {
            var query = _context.HopDongs.AsNoTracking()
                .Where(h => !h.IsDeleted && h.TrangThaiHopDong != TrangThaiHopDong.DaHuy
                    && (h.NguoiThueId == nguoiThueId
                        || h.ChiTietThanhVienHopDongs.Any(m => !m.IsDeleted && m.NguoiThueId == nguoiThueId)));

            if (allowedBranchIds != null)
            {
                query = query.Where(h => allowedBranchIds.Contains(h.PhongTro.ChiNhanhId));
            }

            var rows = await query
                .OrderByDescending(h => h.ThoiDiemBatDau).ThenByDescending(h => h.HopDongId)
                .Select(h => new LichSuThueRow
                {
                    HopDongId = h.HopDongId,
                    MaHopDong = h.MaHopDong,
                    LaNguoiDungTen = h.NguoiThueId == nguoiThueId,
                    SoPhong = h.PhongTro.SoPhong,
                    TenChiNhanh = h.PhongTro.ChiNhanh.TenChiNhanh,
                    ThoiDiemBatDau = h.ThoiDiemBatDau,
                    ThoiDiemKetThuc = h.ThoiDiemKetThuc,
                    TrangThai = (int)h.TrangThaiHopDong
                })
                .ToListAsync(ct);

            if (rows.Count == 0)
            {
                return rows;
            }

            var ids = rows.Select(r => r.HopDongId).ToList();
            var invoices = await _context.HoaDons.AsNoTracking()
                .Where(d => ids.Contains(d.HopDongId) && !d.IsDeleted && d.TrangThaiPhatHanh == TrangThaiPhatHanhHoaDon.DaGui)
                .Select(d => new
                {
                    d.HopDongId,
                    d.TongTien,
                    DaThu = d.LichSuThanhToans.Where(l => !l.IsDeleted).Sum(l => l.SoTienThanhToan)
                })
                .ToListAsync(ct);

            var conNoTheoHopDong = invoices
                .GroupBy(d => d.HopDongId)
                .ToDictionary(g => g.Key, g => g.Sum(d => Math.Max(0m, d.TongTien - d.DaThu)));

            foreach (var row in rows)
            {
                row.ConNo = conNoTheoHopDong.TryGetValue(row.HopDongId, out var no) ? no : 0m;
            }

            return rows;
        }

        public async Task<IReadOnlyList<NoCuRow>> GetOldDebtsAsync(int nguoiThueId, IReadOnlyCollection<int>? allowedBranchIds, CancellationToken ct = default)
        {
            var query = _context.HoaDons.AsNoTracking()
                .Where(d => !d.IsDeleted
                    && d.TrangThaiPhatHanh == TrangThaiPhatHanhHoaDon.DaGui
                    && d.HopDong.TrangThaiHopDong == TrangThaiHopDong.DaKetThuc
                    && !d.HopDong.IsDeleted
                    && d.HopDong.NguoiThueId == nguoiThueId);

            if (allowedBranchIds != null)
            {
                query = query.Where(d => allowedBranchIds.Contains(d.HopDong.PhongTro.ChiNhanhId));
            }

            var rows = await query
                .OrderBy(d => d.Nam).ThenBy(d => d.Thang).ThenBy(d => d.HoaDonId)
                .Select(d => new
                {
                    d.MaHoaDon,
                    d.HopDong.MaHopDong,
                    d.Thang,
                    d.Nam,
                    d.TongTien,
                    DaThu = d.LichSuThanhToans.Where(l => !l.IsDeleted).Sum(l => l.SoTienThanhToan)
                })
                .ToListAsync(ct);

            return rows
                .Where(r => r.TongTien - r.DaThu > 0)
                .Select(r => new NoCuRow
                {
                    MaHoaDon = r.MaHoaDon,
                    MaHopDong = r.MaHopDong,
                    Thang = r.Thang,
                    Nam = r.Nam,
                    ConLai = r.TongTien - r.DaThu
                })
                .ToList();
        }

        public async Task<NguoiThue?> GetNguoiThueAsync(int nguoiThueId, CancellationToken ct = default)
        {
            return await _context.NguoiThues.AsNoTracking()
                .FirstOrDefaultAsync(x => x.NguoiThueId == nguoiThueId && !x.IsDeleted, ct);
        }
    }
}
