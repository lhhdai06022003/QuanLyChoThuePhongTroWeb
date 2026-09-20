using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using QuanLyChoThuePhongTroWeb.Application.Features.Dashboard.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.Dashboard.Persistence;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Features
{
    public class DashboardStore : IDashboardStore
    {
        private readonly ApplicationDbContext _context;

        public DashboardStore(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IReadOnlyList<BranchLookupItemDto>> GetActiveBranchesAsync(CancellationToken cancellationToken = default)
        {
            return await _context.ChiNhanhs
                .Where(c => !c.IsDeleted)
                .Select(c => new BranchLookupItemDto
                {
                    Id = c.ChiNhanhId,
                    Ten = c.TenChiNhanh
                })
                .ToListAsync(cancellationToken);
        }

        public async Task<(int Trong, int DaThue, int BaoTri)> GetRoomStatusCountsAsync(int? branchId, CancellationToken cancellationToken = default)
        {
            var query = _context.PhongTros.Where(p => !p.IsDeleted);
            if (branchId.HasValue)
            {
                query = query.Where(p => p.ChiNhanhId == branchId.Value);
            }

            var phongStats = await query
                .GroupBy(p => p.TrangThai)
                .Select(g => new { TrangThai = g.Key, Count = g.Count() })
                .ToListAsync(cancellationToken);

            int trong = phongStats.FirstOrDefault(s => s.TrangThai == TrangThaiPhong.Trong)?.Count ?? 0;
            int daThue = phongStats.FirstOrDefault(s => s.TrangThai == TrangThaiPhong.DaThue)?.Count ?? 0;
            int baoTri = phongStats.FirstOrDefault(s => s.TrangThai == TrangThaiPhong.BaoTri)?.Count ?? 0;

            return (trong, daThue, baoTri);
        }

        public async Task<int> CountActiveContractsAsync(int? branchId, CancellationToken cancellationToken = default)
        {
            var query = _context.HopDongs
                .Where(h => !h.IsDeleted && h.TrangThaiHopDong == TrangThaiHopDong.DangHoatDong);

            if (branchId.HasValue)
            {
                query = query.Where(h => h.PhongTro.ChiNhanhId == branchId.Value);
            }

            return await query.CountAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<DashboardInvoiceStatDto>> GetInvoiceMonthlyStatsAsync(int? branchId, int year, CancellationToken cancellationToken = default)
        {
            var query = _context.HoaDons
                .Where(h => !h.IsDeleted && h.Nam == year);

            if (branchId.HasValue)
            {
                query = query.Where(h => h.HopDong.PhongTro.ChiNhanhId == branchId.Value);
            }

            return await query
                .GroupBy(h => new { h.Thang, h.TrangThaiHoaDon })
                .Select(g => new DashboardInvoiceStatDto
                {
                    Thang = g.Key.Thang,
                    TrangThai = g.Key.TrangThaiHoaDon,
                    TongTien = g.Sum(h => h.TongTien)
                })
                .ToListAsync(cancellationToken);
        }

        public async Task<int> CountUnpaidRoomsAsync(int? branchId, CancellationToken cancellationToken = default)
        {
            var query = _context.HoaDons
                .Where(h => !h.IsDeleted && h.TrangThaiHoaDon == TrangThaiHoaDon.ChuaThanhToan);

            if (branchId.HasValue)
            {
                query = query.Where(h => h.HopDong.PhongTro.ChiNhanhId == branchId.Value);
            }

            return await query
                .Select(h => h.HopDong.PhongTroId)
                .Distinct()
                .CountAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<DashboardContractUtilityCheckDto>> GetActiveContractsForUtilityCheckAsync(int? branchId, CancellationToken cancellationToken = default)
        {
            var query = _context.HopDongs
                .Where(h => !h.IsDeleted && h.TrangThaiHopDong == TrangThaiHopDong.DangHoatDong);

            if (branchId.HasValue)
            {
                query = query.Where(h => h.PhongTro.ChiNhanhId == branchId.Value);
            }

            return await query
                .Select(h => new DashboardContractUtilityCheckDto
                {
                    PhongTroId = h.PhongTroId,
                    ThoiDiemBatDau = h.ThoiDiemBatDau,
                    ChiNhanhId = h.PhongTro.ChiNhanhId,
                    TenChiNhanh = h.PhongTro.ChiNhanh.TenChiNhanh
                })
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<DashboardRecordedUtilityDto>> GetRecordedUtilitiesAsync(DateTime fromDate, CancellationToken cancellationToken = default)
        {
            return await _context.DichVuDienNuocCuaPhongs
                .Where(d => !d.IsDeleted &&
                       (d.Nam > fromDate.Year || (d.Nam == fromDate.Year && d.Thang >= fromDate.Month)))
                .Select(d => new DashboardRecordedUtilityDto
                {
                    PhongTroId = d.PhongTroId,
                    Thang = d.Thang,
                    Nam = d.Nam
                })
                .ToListAsync(cancellationToken);
        }

        public async Task<int> CountExpiringContractsAsync(int? branchId, DateTime today, DateTime limitDate, CancellationToken cancellationToken = default)
        {
            var query = _context.HopDongs
                .Where(h => !h.IsDeleted &&
                            h.TrangThaiHopDong == TrangThaiHopDong.DangHoatDong &&
                            h.ThoiDiemKetThuc.HasValue &&
                            h.ThoiDiemKetThuc.Value <= limitDate &&
                            h.ThoiDiemKetThuc.Value >= today);

            if (branchId.HasValue)
            {
                query = query.Where(h => h.PhongTro.ChiNhanhId == branchId.Value);
            }

            return await query.CountAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<DashboardUnpaidGroupDto>> GetUnpaidInvoicesGroupedAsync(int? branchId, CancellationToken cancellationToken = default)
        {
            var query = _context.HoaDons
                .Where(h => !h.IsDeleted && h.TrangThaiHoaDon == TrangThaiHoaDon.ChuaThanhToan);

            if (branchId.HasValue)
            {
                query = query.Where(h => h.HopDong.PhongTro.ChiNhanhId == branchId.Value);
            }

            return await query
                .GroupBy(h => new { h.Nam, h.Thang, h.HopDong.PhongTro.ChiNhanhId, TenChiNhanh = h.HopDong.PhongTro.ChiNhanh.TenChiNhanh })
                .Select(g => new DashboardUnpaidGroupDto
                {
                    Nam = g.Key.Nam,
                    Thang = g.Key.Thang,
                    ChiNhanhId = g.Key.ChiNhanhId,
                    TenChiNhanh = g.Key.TenChiNhanh,
                    Count = g.Count()
                })
                .OrderBy(g => g.Nam).ThenBy(g => g.Thang).ThenBy(g => g.TenChiNhanh)
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<DashboardRecentPaymentDto>> GetRecentPaymentsAsync(int? branchId, int count = 5, CancellationToken cancellationToken = default)
        {
            var query = _context.LichSuThanhToans
                .Include(l => l.HoaDon).ThenInclude(h => h.HopDong).ThenInclude(hd => hd.PhongTro)
                .Where(l => !l.IsDeleted);

            if (branchId.HasValue)
            {
                query = query.Where(l => l.HoaDon.HopDong.PhongTro.ChiNhanhId == branchId.Value);
            }

            return await query
                .OrderByDescending(l => l.NgayThanhToan)
                .Take(count)
                .Select(l => new DashboardRecentPaymentDto
                {
                    NgayThanhToan = l.NgayThanhToan,
                    SoPhong = l.HoaDon.HopDong.PhongTro.SoPhong,
                    SoTienThanhToan = l.SoTienThanhToan,
                    PhuongThuc = l.PhuongThucThanhToan
                })
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<DashboardRecentContractDto>> GetRecentContractsAsync(int? branchId, int count = 5, CancellationToken cancellationToken = default)
        {
            var query = _context.HopDongs
                .Include(h => h.PhongTro)
                .Include(h => h.NguoiThue)
                .Where(h => !h.IsDeleted);

            if (branchId.HasValue)
            {
                query = query.Where(h => h.PhongTro.ChiNhanhId == branchId.Value);
            }

            return await query
                .OrderByDescending(h => h.NgayTao)
                .Take(count)
                .Select(h => new DashboardRecentContractDto
                {
                    NgayTao = h.NgayTao,
                    SoPhong = h.PhongTro.SoPhong,
                    HoVaTen = h.NguoiThue.HoVaTen,
                    TienThuePhong = h.TienThuePhong
                })
                .ToListAsync(cancellationToken);
        }
    }
}
