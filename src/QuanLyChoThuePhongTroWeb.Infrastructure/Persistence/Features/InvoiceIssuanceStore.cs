using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Persistence;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Features
{
    public class InvoiceIssuanceStore : IInvoiceIssuanceStore
    {
        private readonly ApplicationDbContext _context;

        public InvoiceIssuanceStore(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IReadOnlyList<int>> LockContractsAsync(IReadOnlyList<int> hopDongIds, CancellationToken ct = default)
        {
            if (hopDongIds == null || hopDongIds.Count == 0)
            {
                return Array.Empty<int>();
            }

            var orderedIds = hopDongIds.Distinct().OrderBy(x => x).ToArray();
            var lockedIds = await _context.HopDongs
                .FromSqlInterpolated($"SELECT * FROM hop_dong WHERE \"HopDongId\" = ANY({orderedIds}) AND \"IsDeleted\" = false ORDER BY \"HopDongId\" FOR UPDATE")
                .Select(x => x.HopDongId)
                .ToListAsync(ct);

            return lockedIds;
        }

        public async Task<HoaDon?> GetInvoiceForUpdateAsync(int hoaDonId, CancellationToken ct = default)
        {
            return await _context.HoaDons
                .FromSqlInterpolated($"SELECT * FROM hoa_don WHERE \"HoaDonId\" = {hoaDonId} AND \"IsDeleted\" = false FOR UPDATE")
                .Include(h => h.ChiTietHoaDonDichVus)
                .Include(h => h.DichVuDienNuocCuaPhong)
                .Include(h => h.HopDong)
                    .ThenInclude(hd => hd.PhongTro)
                .Include(h => h.HopDong)
                    .ThenInclude(hd => hd.NguoiThue)
                .FirstOrDefaultAsync(ct);
        }

        public async Task<int> CountCancelledInvoicesAsync(int hopDongId, int thang, int nam, CancellationToken ct = default)
        {
            return await _context.HoaDons
                .Where(h => h.HopDongId == hopDongId && h.Thang == thang && h.Nam == nam && h.TrangThaiPhatHanh == TrangThaiPhatHanhHoaDon.DaHuy && h.IsDeleted)
                .CountAsync(ct);
        }

        public async Task<bool> HasOtherActiveInvoiceAsync(int hopDongId, int thang, int nam, int excludeHoaDonId, CancellationToken ct = default)
        {
            return await _context.HoaDons
                .AnyAsync(h => h.HopDongId == hopDongId && h.Thang == thang && h.Nam == nam && !h.IsDeleted && h.HoaDonId != excludeHoaDonId, ct);
        }

        public async Task AddInvoiceAsync(HoaDon hoaDon, CancellationToken ct = default)
        {
            await _context.HoaDons.AddAsync(hoaDon, ct);
        }

        public async Task<IReadOnlyList<YeuCauSuCo>> GetBillableIncidentsInPeriodAsync(IReadOnlyList<int> roomIds, DateTime startUtc, DateTime nextMonthStartUtc, CancellationToken ct = default)
        {
            if (roomIds == null || roomIds.Count == 0)
            {
                return Array.Empty<YeuCauSuCo>();
            }

            var distinctRoomIds = roomIds.Distinct().ToArray();
            return await _context.YeuCauSuCos
                .Where(sc => distinctRoomIds.Contains(sc.PhongTroId)
                             && !sc.IsDeleted
                             && sc.CongVaoHoaDon
                             && sc.ChiPhiSuaChua > 0
                             && sc.TrangThai == TrangThaiSuCo.DaHoanThanh
                             && sc.NgayXuLy.HasValue
                             && sc.NgayXuLy.Value >= startUtc
                             && sc.NgayXuLy.Value < nextMonthStartUtc)
                .ToListAsync(ct);
        }

        public async Task<HoaDon?> GetActiveInvoiceForTenantRoomPeriodForUpdateAsync(int phongTroId, int nguoiThueId, int thang, int nam, CancellationToken ct = default)
        {
            return await _context.HoaDons
                .FromSqlInterpolated($"SELECT * FROM hoa_don WHERE \"Thang\" = {thang} AND \"Nam\" = {nam} AND \"IsDeleted\" = false AND \"HopDongId\" IN (SELECT \"HopDongId\" FROM hop_dong WHERE \"PhongTroId\" = {phongTroId} AND \"NguoiThueId\" = {nguoiThueId} AND \"IsDeleted\" = false) FOR UPDATE")
                .Include(h => h.ChiTietHoaDonDichVus)
                .Include(h => h.HopDong)
                .FirstOrDefaultAsync(ct);
        }

        public async Task<(int? MeterPeriodId, int ChiNhanhId)?> GetInvoiceLockTargetsAsync(int hoaDonId, CancellationToken ct = default)
        {
            var result = await _context.HoaDons
                .AsNoTracking()
                .Where(h => h.HoaDonId == hoaDonId && !h.IsDeleted)
                .Select(h => new
                {
                    h.DichVuDienNuocCuaPhongId,
                    BranchId = h.HopDong.PhongTro.ChiNhanhId
                })
                .FirstOrDefaultAsync(ct);

            if (result == null)
            {
                return null;
            }

            return (result.DichVuDienNuocCuaPhongId, result.BranchId);
        }

        public async Task<TrangThaiGhiNhan?> LockMeterPeriodAsync(int meterPeriodId, CancellationToken ct = default)
        {
            var records = await _context.DichVuDienNuocCuaPhongs
                .FromSqlInterpolated($"SELECT * FROM dich_vu_dien_nuoc_cua_phong WHERE \"DichVuDienNuocCuaPhongId\" = {meterPeriodId} AND \"IsDeleted\" = false FOR UPDATE")
                .AsNoTracking()
                .Select(x => (TrangThaiGhiNhan?)x.TrangThaiGhiNhan)
                .ToListAsync(ct);

            return records.FirstOrDefault();
        }

        public async Task LockMeterPeriodsForRoomsAsync(IReadOnlyList<int> roomIds, int thang, int nam, CancellationToken ct = default)
        {
            if (roomIds.Count == 0)
            {
                return;
            }

            var ids = roomIds.Distinct().OrderBy(x => x).ToArray();
            // Chỉ cần giữ khóa hàng đến hết giao dịch; ORDER BY id để mọi giao dịch khóa cùng thứ tự, tránh deadlock.
            await _context.DichVuDienNuocCuaPhongs
                .FromSqlInterpolated($"SELECT * FROM dich_vu_dien_nuoc_cua_phong WHERE \"PhongTroId\" = ANY({ids}) AND \"Thang\" = {thang} AND \"Nam\" = {nam} AND \"IsDeleted\" = false ORDER BY \"DichVuDienNuocCuaPhongId\" FOR UPDATE")
                .AsNoTracking()
                .Select(x => x.DichVuDienNuocCuaPhongId)
                .ToListAsync(ct);
        }

        public async Task<IReadOnlyList<int>> GetContractIdsForTenantRoomAsync(int phongTroId, int nguoiThueId, CancellationToken ct = default)
        {
            var contracts = await _context.HopDongs
                .Where(h => h.PhongTroId == phongTroId && h.NguoiThueId == nguoiThueId && !h.IsDeleted)
                .Select(h => h.HopDongId)
                .ToListAsync(ct);

            return contracts;
        }
    }
}
