using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using QuanLyChoThuePhongTroWeb.Application.Features.AiAssistants.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.AiAssistants.Persistence;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Features
{
    public class AiAssistantStore : IAiAssistantStore
    {
        private readonly ApplicationDbContext _context;

        public AiAssistantStore(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IReadOnlyList<AiRoomRow>> GetRentedRoomsWithoutMeterReadingAsync(int thang, int nam, IReadOnlyCollection<int>? allowedBranchIds, CancellationToken ct = default)
        {
            var query = _context.PhongTros
                .AsNoTracking()
                .Where(p => !p.IsDeleted && p.TrangThai == TrangThaiPhong.DaThue)
                .Where(p => !_context.DichVuDienNuocCuaPhongs.Any(dv =>
                    dv.PhongTroId == p.PhongTroId &&
                    dv.Thang == thang &&
                    dv.Nam == nam &&
                    !dv.IsDeleted));

            if (allowedBranchIds != null)
            {
                query = query.Where(p => allowedBranchIds.Contains(p.ChiNhanhId));
            }

            return await ToRoomRowsAsync(query, ct);
        }

        public async Task<IReadOnlyList<AiUnpaidInvoiceRow>> GetUnpaidInvoicesAsync(int? thang, int? nam, int? phongTroId, IReadOnlyCollection<int>? allowedBranchIds, CancellationToken ct = default)
        {
            var query = UnpaidInvoices(thang, nam);

            if (phongTroId.HasValue)
            {
                query = query.Where(h => h.HopDong.PhongTroId == phongTroId.Value);
            }

            if (allowedBranchIds != null)
            {
                query = query.Where(h => allowedBranchIds.Contains(h.HopDong.PhongTro.ChiNhanhId));
            }

            return await ToInvoiceRowsAsync(query, ct);
        }

        public async Task<AiRevenueTotalRow> GetRevenueAsync(DateTime fromUtc, DateTime toExclusiveUtc, IReadOnlyCollection<int>? allowedBranchIds, CancellationToken ct = default)
        {
            var query = _context.LichSuThanhToans
                .AsNoTracking()
                .Where(l => !l.IsDeleted && !l.HoaDon.IsDeleted &&
                            l.NgayThanhToan >= fromUtc && l.NgayThanhToan < toExclusiveUtc);

            if (allowedBranchIds != null)
            {
                query = query.Where(l => allowedBranchIds.Contains(l.HoaDon.HopDong.PhongTro.ChiNhanhId));
            }

            var total = await query.SumAsync(l => (decimal?)l.SoTienThanhToan, ct) ?? 0m;
            var count = await query.CountAsync(ct);
            return new AiRevenueTotalRow(total, count);
        }

        public async Task<IReadOnlyList<AiRoomRow>> GetVacantRoomsAsync(decimal? giaToiDa, IReadOnlyCollection<int>? allowedBranchIds, CancellationToken ct = default)
        {
            var query = _context.PhongTros
                .AsNoTracking()
                .Where(p => !p.IsDeleted && p.TrangThai == TrangThaiPhong.Trong);

            if (giaToiDa.HasValue)
            {
                query = query.Where(p => p.GiaThue <= giaToiDa.Value);
            }

            if (allowedBranchIds != null)
            {
                query = query.Where(p => allowedBranchIds.Contains(p.ChiNhanhId));
            }

            return await ToRoomRowsAsync(query, ct);
        }

        public async Task<IReadOnlyList<AiExpiringContractRow>> GetExpiringContractsAsync(DateTime fromUtc, DateTime toUtc, IReadOnlyCollection<int>? allowedBranchIds, CancellationToken ct = default)
        {
            var query = _context.HopDongs
                .AsNoTracking()
                .Where(h => !h.IsDeleted &&
                            h.TrangThaiHopDong == TrangThaiHopDong.DangHoatDong &&
                            h.ThoiDiemKetThuc != null &&
                            h.ThoiDiemKetThuc >= fromUtc &&
                            h.ThoiDiemKetThuc <= toUtc);

            if (allowedBranchIds != null)
            {
                query = query.Where(h => allowedBranchIds.Contains(h.PhongTro.ChiNhanhId));
            }

            var rows = await query
                .OrderBy(h => h.ThoiDiemKetThuc)
                .Select(h => new
                {
                    h.PhongTro.SoPhong,
                    h.PhongTro.ChiNhanh.TenChiNhanh,
                    h.NguoiThue.HoVaTen,
                    KetThuc = h.ThoiDiemKetThuc!.Value
                })
                .ToListAsync(ct);

            return rows.Select(r => new AiExpiringContractRow(r.SoPhong, r.TenChiNhanh, r.HoVaTen, r.KetThuc)).ToList();
        }

        public async Task<IReadOnlyList<AiTenantLookupRow>> SearchTenantsAsync(string tuKhoa, IReadOnlyCollection<int>? allowedBranchIds, CancellationToken ct = default)
        {
            var k = tuKhoa.Trim().ToLower();
            var query = _context.HopDongs
                .AsNoTracking()
                .Where(h => !h.IsDeleted && !h.NguoiThue.IsDeleted &&
                            (h.NguoiThue.HoVaTen.ToLower().Contains(k) || h.PhongTro.SoPhong.ToLower().Contains(k)));

            if (allowedBranchIds != null)
            {
                query = query.Where(h => allowedBranchIds.Contains(h.PhongTro.ChiNhanhId));
            }

            var rows = await query
                .OrderBy(h => h.PhongTro.ChiNhanh.TenChiNhanh)
                .ThenBy(h => h.PhongTro.SoPhong)
                .ThenBy(h => h.HopDongId)
                .Select(h => new
                {
                    h.PhongTro.SoPhong,
                    h.PhongTro.ChiNhanh.TenChiNhanh,
                    h.NguoiThue.HoVaTen,
                    h.NguoiThue.SoDienThoai,
                    h.TrangThaiHopDong
                })
                .ToListAsync(ct);

            return rows.Select(r => new AiTenantLookupRow(r.SoPhong, r.TenChiNhanh, r.HoVaTen, r.SoDienThoai, r.TrangThaiHopDong)).ToList();
        }

        public async Task<IReadOnlyList<AiRoomRow>> FindRoomsByNumberAsync(string soPhong, string? tenChiNhanh, IReadOnlyCollection<int>? allowedBranchIds, CancellationToken ct = default)
        {
            var so = soPhong.Trim().ToLower();
            var query = _context.PhongTros
                .AsNoTracking()
                .Where(p => !p.IsDeleted && p.SoPhong.Trim().ToLower() == so);

            if (!string.IsNullOrWhiteSpace(tenChiNhanh))
            {
                var ten = tenChiNhanh.Trim().ToLower();
                query = query.Where(p => p.ChiNhanh.TenChiNhanh.ToLower().Contains(ten));
            }

            if (allowedBranchIds != null)
            {
                query = query.Where(p => allowedBranchIds.Contains(p.ChiNhanhId));
            }

            return await ToRoomRowsAsync(query, ct);
        }

        public async Task<IReadOnlyList<AiBranchRevenueRow>> GetRevenueByBranchAsync(int thang, int nam, IReadOnlyCollection<int>? allowedBranchIds, CancellationToken ct = default)
        {
            // Doanh thu theo kỳ hóa đơn (HoaDon.Thang/Nam), cùng cách tính với ô doanh thu tháng của Dashboard.
            var query = _context.LichSuThanhToans
                .AsNoTracking()
                .Where(l => !l.IsDeleted && !l.HoaDon.IsDeleted && l.HoaDon.Thang == thang && l.HoaDon.Nam == nam);

            if (allowedBranchIds != null)
            {
                query = query.Where(l => allowedBranchIds.Contains(l.HoaDon.HopDong.PhongTro.ChiNhanhId));
            }

            var rows = await query
                .GroupBy(l => new
                {
                    l.HoaDon.HopDong.PhongTro.ChiNhanhId,
                    l.HoaDon.HopDong.PhongTro.ChiNhanh.TenChiNhanh
                })
                .Select(g => new
                {
                    g.Key.ChiNhanhId,
                    g.Key.TenChiNhanh,
                    Tong = g.Sum(l => l.SoTienThanhToan),
                    SoGiaoDich = g.Count()
                })
                .ToListAsync(ct);

            // Liệt kê cả chi nhánh trong phạm vi chưa có giao dịch (doanh thu 0).
            var branchQuery = _context.ChiNhanhs.AsNoTracking().Where(c => !c.IsDeleted);
            if (allowedBranchIds != null)
            {
                branchQuery = branchQuery.Where(c => allowedBranchIds.Contains(c.ChiNhanhId));
            }
            var branches = await branchQuery.Select(c => new { c.ChiNhanhId, c.TenChiNhanh }).ToListAsync(ct);

            var result = rows
                .Select(r => new AiBranchRevenueRow(r.ChiNhanhId, r.TenChiNhanh, r.Tong, r.SoGiaoDich))
                .ToList();
            var withRevenue = rows.Select(r => r.ChiNhanhId).ToHashSet();
            result.AddRange(branches
                .Where(b => !withRevenue.Contains(b.ChiNhanhId))
                .Select(b => new AiBranchRevenueRow(b.ChiNhanhId, b.TenChiNhanh, 0m, 0)));

            return result.OrderBy(r => r.TenChiNhanh).ThenBy(r => r.ChiNhanhId).ToList();
        }

        public async Task<IReadOnlyList<AiMeterReadingRow>> GetMeterReadingsAsync(IReadOnlyCollection<int> phongTroIds, int thang, int nam, CancellationToken ct = default)
        {
            var rows = await _context.DichVuDienNuocCuaPhongs
                .AsNoTracking()
                .Where(d => !d.IsDeleted && phongTroIds.Contains(d.PhongTroId) && d.Thang == thang && d.Nam == nam)
                .Select(d => new
                {
                    d.DichVuDienNuocCuaPhongId,
                    d.PhongTroId,
                    d.PhongTro.SoPhong,
                    d.PhongTro.ChiNhanh.TenChiNhanh,
                    d.Thang,
                    d.Nam,
                    d.ChiSoDienCu,
                    d.ChiSoDienMoi,
                    d.ChiSoNuocCu,
                    d.ChiSoNuocMoi
                })
                .ToListAsync(ct);

            // Mỗi phòng lấy bản ghi mới nhất (Id lớn nhất).
            return rows
                .GroupBy(r => r.PhongTroId)
                .Select(g => g.OrderByDescending(r => r.DichVuDienNuocCuaPhongId).First())
                .OrderBy(r => r.TenChiNhanh)
                .ThenBy(r => r.SoPhong)
                .Select(r => new AiMeterReadingRow(r.PhongTroId, r.SoPhong, r.TenChiNhanh, r.Thang, r.Nam, r.ChiSoDienCu, r.ChiSoDienMoi, r.ChiSoNuocCu, r.ChiSoNuocMoi))
                .ToList();
        }

        public async Task<IReadOnlyList<AiTenantContractRow>> GetActiveContractsOfTenantAsync(int nguoiThueId, CancellationToken ct = default)
        {
            var rows = await _context.HopDongs
                .AsNoTracking()
                .Where(h => !h.IsDeleted && h.NguoiThueId == nguoiThueId && h.TrangThaiHopDong == TrangThaiHopDong.DangHoatDong)
                .OrderBy(h => h.ThoiDiemBatDau)
                .ThenBy(h => h.HopDongId)
                .Select(h => new
                {
                    h.MaHopDong,
                    h.PhongTroId,
                    h.PhongTro.SoPhong,
                    h.PhongTro.ChiNhanh.TenChiNhanh,
                    h.ThoiDiemBatDau,
                    h.ThoiDiemKetThuc,
                    h.TienThuePhong,
                    h.TienCocPhong
                })
                .ToListAsync(ct);

            return rows
                .Select(r => new AiTenantContractRow(r.MaHopDong, r.PhongTroId, r.SoPhong, r.TenChiNhanh, r.ThoiDiemBatDau, r.ThoiDiemKetThuc, r.TienThuePhong, r.TienCocPhong))
                .ToList();
        }

        public async Task<IReadOnlyList<AiUnpaidInvoiceRow>> GetTenantUnpaidInvoicesAsync(int nguoiThueId, int? thang, int? nam, CancellationToken ct = default)
        {
            var query = UnpaidInvoices(thang, nam).Where(h => h.HopDong.NguoiThueId == nguoiThueId);
            return await ToInvoiceRowsAsync(query, ct);
        }

        // Hóa đơn đã gửi cho khách và chưa thanh toán đủ; không xét trạng thái phòng.
        private IQueryable<HoaDon> UnpaidInvoices(int? thang, int? nam)
        {
            var query = _context.HoaDons
                .AsNoTracking()
                .Where(h => !h.IsDeleted &&
                            h.TrangThaiPhatHanh == TrangThaiPhatHanhHoaDon.DaGui &&
                            h.TrangThaiHoaDon != TrangThaiHoaDon.DaThanhToan);

            if (thang.HasValue)
            {
                query = query.Where(h => h.Thang == thang.Value);
            }

            if (nam.HasValue)
            {
                query = query.Where(h => h.Nam == nam.Value);
            }

            return query;
        }

        private static async Task<IReadOnlyList<AiUnpaidInvoiceRow>> ToInvoiceRowsAsync(IQueryable<HoaDon> query, CancellationToken ct)
        {
            var rows = await query
                .Select(h => new
                {
                    h.HoaDonId,
                    h.MaHoaDon,
                    h.HopDong.PhongTroId,
                    h.HopDong.PhongTro.SoPhong,
                    h.HopDong.PhongTro.ChiNhanh.TenChiNhanh,
                    KhachThue = h.HopDong.NguoiThue.HoVaTen,
                    h.Thang,
                    h.Nam,
                    h.TongTien,
                    DaThu = h.LichSuThanhToans.Where(l => !l.IsDeleted).Sum(l => l.SoTienThanhToan)
                })
                .OrderBy(r => r.Nam)
                .ThenBy(r => r.Thang)
                .ThenBy(r => r.TenChiNhanh)
                .ThenBy(r => r.SoPhong)
                .ThenBy(r => r.HoaDonId)
                .ToListAsync(ct);

            return rows
                .Select(r => new AiUnpaidInvoiceRow(r.HoaDonId, r.MaHoaDon, r.PhongTroId, r.SoPhong, r.TenChiNhanh, r.KhachThue, r.Thang, r.Nam, r.TongTien, r.DaThu))
                .ToList();
        }

        private static async Task<IReadOnlyList<AiRoomRow>> ToRoomRowsAsync(IQueryable<PhongTro> query, CancellationToken ct)
        {
            var rows = await query
                .OrderBy(p => p.ChiNhanh.TenChiNhanh)
                .ThenBy(p => p.SoPhong)
                .Select(p => new
                {
                    p.PhongTroId,
                    p.SoPhong,
                    p.TangLau,
                    p.ChiNhanhId,
                    p.ChiNhanh.TenChiNhanh,
                    p.GiaThue
                })
                .ToListAsync(ct);

            return rows
                .Select(r => new AiRoomRow(r.PhongTroId, r.SoPhong, r.TangLau, r.ChiNhanhId, r.TenChiNhanh, r.GiaThue))
                .ToList();
        }
    }
}
