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

        public async Task<IReadOnlyList<BranchLookupItemDto>> GetActiveBranchesAsync(IReadOnlyCollection<int>? allowedBranchIds = null, CancellationToken cancellationToken = default)
        {
            var query = _context.ChiNhanhs.Where(c => !c.IsDeleted);
            if (allowedBranchIds != null)
            {
                query = query.Where(c => allowedBranchIds.Contains(c.ChiNhanhId));
            }

            return await query
                .Select(c => new BranchLookupItemDto
                {
                    Id = c.ChiNhanhId,
                    Ten = c.TenChiNhanh
                })
                .ToListAsync(cancellationToken);
        }

        public async Task<(int Trong, int DaThue, int BaoTri)> GetRoomStatusCountsAsync(int? branchId, IReadOnlyCollection<int>? allowedBranchIds = null, CancellationToken cancellationToken = default)
        {
            var query = _context.PhongTros.Where(p => !p.IsDeleted);
            if (branchId.HasValue)
            {
                query = query.Where(p => p.ChiNhanhId == branchId.Value);
            }

            if (allowedBranchIds != null)
            {
                query = query.Where(p => allowedBranchIds.Contains(p.ChiNhanhId));
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

        public async Task<int> CountActiveContractsAsync(int? branchId, IReadOnlyCollection<int>? allowedBranchIds = null, CancellationToken cancellationToken = default)
        {
            var query = _context.HopDongs
                .Where(h => !h.IsDeleted && h.TrangThaiHopDong == TrangThaiHopDong.DangHoatDong);

            if (branchId.HasValue)
            {
                query = query.Where(h => h.PhongTro.ChiNhanhId == branchId.Value);
            }

            if (allowedBranchIds != null)
            {
                query = query.Where(h => allowedBranchIds.Contains(h.PhongTro.ChiNhanhId));
            }

            return await query.CountAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<DashboardInvoiceStatDto>> GetInvoiceMonthlyStatsAsync(int? branchId, int year, IReadOnlyCollection<int>? allowedBranchIds = null, CancellationToken cancellationToken = default)
        {
            var query = _context.HoaDons
                .Where(h => !h.IsDeleted && h.Nam == year);

            if (branchId.HasValue)
            {
                query = query.Where(h => h.HopDong.PhongTro.ChiNhanhId == branchId.Value);
            }

            if (allowedBranchIds != null)
            {
                query = query.Where(h => allowedBranchIds.Contains(h.HopDong.PhongTro.ChiNhanhId));
            }

            // Lấy từng hóa đơn rồi gom trong bộ nhớ: số hóa đơn một năm của một chi nhánh nhỏ,
            // tránh dịch GroupBy trên cột tính từ subquery ledger.
            var rows = await query
                .Select(h => new
                {
                    h.Thang,
                    h.TrangThaiHoaDon,
                    h.TrangThaiPhatHanh,
                    h.TongTien,
                    DaThu = h.LichSuThanhToans.Where(l => !l.IsDeleted).Sum(l => l.SoTienThanhToan)
                })
                .ToListAsync(cancellationToken);

            return rows
                .GroupBy(h => h.Thang)
                .Select(g => new DashboardInvoiceStatDto
                {
                    Thang = g.Key,
                    DaThu = g.Sum(h => h.DaThu),
                    ChoThu = g
                        .Where(h => h.TrangThaiPhatHanh == TrangThaiPhatHanhHoaDon.DaGui
                                    && h.TrangThaiHoaDon != TrangThaiHoaDon.DaThanhToan)
                        .Sum(h => h.TongTien - h.DaThu)
                })
                .ToList();
        }

        public async Task<int> CountUnpaidRoomsAsync(int? branchId, IReadOnlyCollection<int>? allowedBranchIds = null, CancellationToken cancellationToken = default)
        {
            var query = _context.HoaDons.DaGuiChuaThuDu();

            if (branchId.HasValue)
            {
                query = query.Where(h => h.HopDong.PhongTro.ChiNhanhId == branchId.Value);
            }

            if (allowedBranchIds != null)
            {
                query = query.Where(h => allowedBranchIds.Contains(h.HopDong.PhongTro.ChiNhanhId));
            }

            return await query
                .Select(h => h.HopDong.PhongTroId)
                .Distinct()
                .CountAsync(cancellationToken);
        }

        public async Task<DashboardOverdueSummaryDto> GetOverdueSummaryAsync(int? branchId, DateTime nowUtc, IReadOnlyCollection<int>? allowedBranchIds = null, CancellationToken cancellationToken = default)
        {
            var query = _context.HoaDons.QuaHan(nowUtc);

            if (branchId.HasValue)
            {
                query = query.Where(h => h.HopDong.PhongTro.ChiNhanhId == branchId.Value);
            }

            if (allowedBranchIds != null)
            {
                query = query.Where(h => allowedBranchIds.Contains(h.HopDong.PhongTro.ChiNhanhId));
            }

            var rows = await query
                .Select(h => new
                {
                    h.TongTien,
                    DaThu = h.LichSuThanhToans.Where(l => !l.IsDeleted).Sum(l => l.SoTienThanhToan)
                })
                .ToListAsync(cancellationToken);

            return new DashboardOverdueSummaryDto
            {
                SoHoaDon = rows.Count,
                TongConNo = rows.Sum(r => r.TongTien - r.DaThu)
            };
        }

        public async Task<IReadOnlyList<DashboardRecentPaymentDto>> GetRecentPaymentsAsync(int? branchId, int count = 5, IReadOnlyCollection<int>? allowedBranchIds = null, CancellationToken cancellationToken = default)
        {
            var query = _context.LichSuThanhToans
                .Include(l => l.HoaDon).ThenInclude(h => h.HopDong).ThenInclude(hd => hd.PhongTro)
                .Where(l => !l.IsDeleted);

            if (branchId.HasValue)
            {
                query = query.Where(l => l.HoaDon.HopDong.PhongTro.ChiNhanhId == branchId.Value);
            }

            if (allowedBranchIds != null)
            {
                query = query.Where(l => allowedBranchIds.Contains(l.HoaDon.HopDong.PhongTro.ChiNhanhId));
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

        public async Task<IReadOnlyList<DashboardRecentContractDto>> GetRecentContractsAsync(int? branchId, int count = 5, IReadOnlyCollection<int>? allowedBranchIds = null, CancellationToken cancellationToken = default)
        {
            var query = _context.HopDongs
                .Include(h => h.PhongTro)
                .Include(h => h.NguoiThue)
                .Where(h => !h.IsDeleted);

            if (branchId.HasValue)
            {
                query = query.Where(h => h.PhongTro.ChiNhanhId == branchId.Value);
            }

            if (allowedBranchIds != null)
            {
                query = query.Where(h => allowedBranchIds.Contains(h.PhongTro.ChiNhanhId));
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
