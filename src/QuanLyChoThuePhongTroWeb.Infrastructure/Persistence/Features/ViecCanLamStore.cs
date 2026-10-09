using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using QuanLyChoThuePhongTroWeb.Application.Features.ViecCanLam.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.ViecCanLam.Persistence;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Features
{
    public class ViecCanLamStore : IViecCanLamStore
    {
        private readonly ApplicationDbContext _context;

        public ViecCanLamStore(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IReadOnlyList<HopDongChoChotChiSoRow>> GetHopDongDangHoatDongAsync(int? branchId, IReadOnlyCollection<int>? allowedBranchIds, CancellationToken ct = default)
        {
            var query = _context.HopDongs.AsNoTracking()
                .Where(h => !h.IsDeleted && h.TrangThaiHopDong == TrangThaiHopDong.DangHoatDong);

            if (branchId.HasValue)
            {
                query = query.Where(h => h.PhongTro.ChiNhanhId == branchId.Value);
            }

            if (allowedBranchIds != null)
            {
                query = query.Where(h => allowedBranchIds.Contains(h.PhongTro.ChiNhanhId));
            }

            return await query
                .Select(h => new HopDongChoChotChiSoRow
                {
                    PhongTroId = h.PhongTroId,
                    ThoiDiemBatDau = h.ThoiDiemBatDau,
                    ChiNhanhId = h.PhongTro.ChiNhanhId,
                    TenChiNhanh = h.PhongTro.ChiNhanh.TenChiNhanh
                })
                .ToListAsync(ct);
        }

        public async Task<IReadOnlyList<KyChiSoDaChotRow>> GetKyChiSoDaChotAsync(int? branchId, IReadOnlyCollection<int>? allowedBranchIds, int tuThang, int tuNam, CancellationToken ct = default)
        {
            var query = _context.DichVuDienNuocCuaPhongs.AsNoTracking()
                .Where(d => !d.IsDeleted
                            && d.TrangThaiGhiNhan == TrangThaiGhiNhan.DaDuyet
                            && (d.Nam > tuNam || (d.Nam == tuNam && d.Thang >= tuThang)));

            if (branchId.HasValue)
            {
                query = query.Where(d => d.PhongTro.ChiNhanhId == branchId.Value);
            }

            if (allowedBranchIds != null)
            {
                query = query.Where(d => allowedBranchIds.Contains(d.PhongTro.ChiNhanhId));
            }

            return await query
                .Select(d => new KyChiSoDaChotRow
                {
                    PhongTroId = d.PhongTroId,
                    Thang = d.Thang,
                    Nam = d.Nam
                })
                .ToListAsync(ct);
        }

        public async Task<IReadOnlyList<DemTheoKyRow>> DemAnhChiSoChoXacNhanAsync(int? branchId, IReadOnlyCollection<int>? allowedBranchIds, CancellationToken ct = default)
        {
            var query = _context.AnhChiSoDongHos.AsNoTracking()
                .Where(a => !a.IsDeleted
                            && (a.TrangThaiXuLy == TrangThaiXuLyAnhChiSo.DocDuoc
                                || a.TrangThaiXuLy == TrangThaiXuLyAnhChiSo.KhongDocDuoc
                                || a.TrangThaiXuLy == TrangThaiXuLyAnhChiSo.Loi)
                            && !a.DichVuDienNuocCuaPhong.IsDeleted);

            if (branchId.HasValue)
            {
                query = query.Where(a => a.DichVuDienNuocCuaPhong.PhongTro.ChiNhanhId == branchId.Value);
            }

            if (allowedBranchIds != null)
            {
                query = query.Where(a => allowedBranchIds.Contains(a.DichVuDienNuocCuaPhong.PhongTro.ChiNhanhId));
            }

            return await query
                .GroupBy(a => new
                {
                    a.DichVuDienNuocCuaPhong.PhongTro.ChiNhanhId,
                    a.DichVuDienNuocCuaPhong.PhongTro.ChiNhanh.TenChiNhanh,
                    a.DichVuDienNuocCuaPhong.Thang,
                    a.DichVuDienNuocCuaPhong.Nam
                })
                .Select(g => new DemTheoKyRow
                {
                    ChiNhanhId = g.Key.ChiNhanhId,
                    TenChiNhanh = g.Key.TenChiNhanh,
                    Thang = g.Key.Thang,
                    Nam = g.Key.Nam,
                    SoLuong = g.Count()
                })
                .ToListAsync(ct);
        }

        public Task<IReadOnlyList<DemHoaDonRow>> DemHoaDonNhapAsync(int? branchId, IReadOnlyCollection<int>? allowedBranchIds, CancellationToken ct = default)
            => DemHoaDonTheoPhatHanhAsync(branchId, allowedBranchIds,
                _context.HoaDons.Where(h => !h.IsDeleted && h.TrangThaiPhatHanh == TrangThaiPhatHanhHoaDon.Nhap), ct);

        public Task<IReadOnlyList<DemHoaDonRow>> DemHoaDonChoDuyetAsync(int? branchId, IReadOnlyCollection<int>? allowedBranchIds, CancellationToken ct = default)
            => DemHoaDonTheoPhatHanhAsync(branchId, allowedBranchIds,
                _context.HoaDons.Where(h => !h.IsDeleted && h.TrangThaiPhatHanh == TrangThaiPhatHanhHoaDon.ChoDuyet), ct);

        public Task<IReadOnlyList<DemHoaDonRow>> DemHoaDonDaChotChuaGuiAsync(int? branchId, IReadOnlyCollection<int>? allowedBranchIds, CancellationToken ct = default)
            => DemHoaDonTheoPhatHanhAsync(branchId, allowedBranchIds,
                _context.HoaDons.Where(h => !h.IsDeleted
                                            && h.TrangThaiPhatHanh == TrangThaiPhatHanhHoaDon.DaChot
                                            && h.TrangThaiHoaDon != TrangThaiHoaDon.DaThanhToan), ct);

        public async Task<IReadOnlyList<DemChiNhanhRow>> DemMinhChungChoDoiChieuAsync(int? branchId, IReadOnlyCollection<int>? allowedBranchIds, CancellationToken ct = default)
        {
            var query = _context.MinhChungThanhToanHoaDons.AsNoTracking()
                .Where(m => !m.IsDeleted
                            && m.TrangThaiDoiChieu == TrangThaiMinhChungThanhToan.ChoXacNhan
                            && !m.YeuCauThanhToanHoaDon.IsDeleted
                            && !m.YeuCauThanhToanHoaDon.HoaDon.IsDeleted);

            if (branchId.HasValue)
            {
                query = query.Where(m => m.YeuCauThanhToanHoaDon.HoaDon.HopDong.PhongTro.ChiNhanhId == branchId.Value);
            }

            if (allowedBranchIds != null)
            {
                query = query.Where(m => allowedBranchIds.Contains(m.YeuCauThanhToanHoaDon.HoaDon.HopDong.PhongTro.ChiNhanhId));
            }

            return await query
                .GroupBy(m => new
                {
                    m.YeuCauThanhToanHoaDon.HoaDon.HopDong.PhongTro.ChiNhanhId,
                    m.YeuCauThanhToanHoaDon.HoaDon.HopDong.PhongTro.ChiNhanh.TenChiNhanh
                })
                .Select(g => new DemChiNhanhRow
                {
                    ChiNhanhId = g.Key.ChiNhanhId,
                    TenChiNhanh = g.Key.TenChiNhanh,
                    SoLuong = g.Count()
                })
                .ToListAsync(ct);
        }

        public Task<IReadOnlyList<DemHoaDonRow>> DemHoaDonQuaHanAsync(int? branchId, IReadOnlyCollection<int>? allowedBranchIds, DateTime nowUtc, CancellationToken ct = default)
            => DemHoaDonKemConNoAsync(branchId, allowedBranchIds, _context.HoaDons.QuaHan(nowUtc), ct);

        public Task<IReadOnlyList<DemHoaDonRow>> DemHoaDonDaGuiChuaToiHanAsync(int? branchId, IReadOnlyCollection<int>? allowedBranchIds, DateTime nowUtc, CancellationToken ct = default)
            => DemHoaDonKemConNoAsync(branchId, allowedBranchIds, _context.HoaDons.ChuaToiHan(nowUtc), ct);

        public async Task<IReadOnlyList<DemChiNhanhRow>> DemSuCoChoTiepNhanAsync(int? branchId, IReadOnlyCollection<int>? allowedBranchIds, DateTime mocGapUtc, CancellationToken ct = default)
        {
            var query = LocSuCo(_context.YeuCauSuCos.AsNoTracking()
                .Where(s => !s.IsDeleted && s.TrangThai == TrangThaiSuCo.ChoTiepNhan), branchId, allowedBranchIds);

            return await query
                .GroupBy(s => new { s.PhongTro.ChiNhanhId, s.PhongTro.ChiNhanh.TenChiNhanh })
                .Select(g => new DemChiNhanhRow
                {
                    ChiNhanhId = g.Key.ChiNhanhId,
                    TenChiNhanh = g.Key.TenChiNhanh,
                    SoLuong = g.Count(),
                    SoLuongGap = g.Sum(x => x.NgayGui <= mocGapUtc ? 1 : 0)
                })
                .ToListAsync(ct);
        }

        public async Task<IReadOnlyList<DemChiNhanhRow>> DemSuCoXuLyQuaLauAsync(int? branchId, IReadOnlyCollection<int>? allowedBranchIds, DateTime mocUtc, CancellationToken ct = default)
        {
            var query = LocSuCo(_context.YeuCauSuCos.AsNoTracking()
                .Where(s => !s.IsDeleted && s.TrangThai == TrangThaiSuCo.DangXuLy && s.NgayGui <= mocUtc), branchId, allowedBranchIds);

            return await query
                .GroupBy(s => new { s.PhongTro.ChiNhanhId, s.PhongTro.ChiNhanh.TenChiNhanh })
                .Select(g => new DemChiNhanhRow
                {
                    ChiNhanhId = g.Key.ChiNhanhId,
                    TenChiNhanh = g.Key.TenChiNhanh,
                    SoLuong = g.Count()
                })
                .ToListAsync(ct);
        }

        public async Task<IReadOnlyList<DemChiNhanhRow>> DemHopDongSapHetHanAsync(int? branchId, IReadOnlyCollection<int>? allowedBranchIds, DateTime tuUtc, DateTime denTruocUtc, DateTime gapTruocUtc, CancellationToken ct = default)
        {
            var query = _context.HopDongs.AsNoTracking()
                .Where(h => !h.IsDeleted
                            && h.TrangThaiHopDong == TrangThaiHopDong.DangHoatDong
                            && h.ThoiDiemKetThuc != null
                            && h.ThoiDiemKetThuc >= tuUtc
                            && h.ThoiDiemKetThuc < denTruocUtc);

            if (branchId.HasValue)
            {
                query = query.Where(h => h.PhongTro.ChiNhanhId == branchId.Value);
            }

            if (allowedBranchIds != null)
            {
                query = query.Where(h => allowedBranchIds.Contains(h.PhongTro.ChiNhanhId));
            }

            return await query
                .GroupBy(h => new { h.PhongTro.ChiNhanhId, h.PhongTro.ChiNhanh.TenChiNhanh })
                .Select(g => new DemChiNhanhRow
                {
                    ChiNhanhId = g.Key.ChiNhanhId,
                    TenChiNhanh = g.Key.TenChiNhanh,
                    SoLuong = g.Count(),
                    SoLuongGap = g.Sum(x => x.ThoiDiemKetThuc < gapTruocUtc ? 1 : 0)
                })
                .ToListAsync(ct);
        }

        public async Task<IReadOnlyList<HoaDonQuaHanRow>> GetHoaDonQuaHanAsync(int? branchId, IReadOnlyCollection<int>? allowedBranchIds, DateTime nowUtc, int gioiHan, CancellationToken ct = default)
        {
            var query = LocHoaDon(_context.HoaDons.AsNoTracking().QuaHan(nowUtc), branchId, allowedBranchIds);

            return await query
                .OrderBy(h => h.HanThanhToan)
                .ThenBy(h => h.HoaDonId)
                .Take(gioiHan)
                .Select(h => new HoaDonQuaHanRow
                {
                    HoaDonId = h.HoaDonId,
                    MaHoaDon = h.MaHoaDon,
                    SoPhong = h.HopDong.PhongTro.SoPhong,
                    ChiNhanhId = h.HopDong.PhongTro.ChiNhanhId,
                    TenChiNhanh = h.HopDong.PhongTro.ChiNhanh.TenChiNhanh,
                    KhachThue = h.HopDong.NguoiThue.HoVaTen,
                    TongTien = h.TongTien,
                    DaThu = h.LichSuThanhToans.Where(l => !l.IsDeleted).Sum(l => l.SoTienThanhToan),
                    HanThanhToan = h.HanThanhToan!.Value
                })
                .ToListAsync(ct);
        }

        private static IQueryable<HoaDon> LocHoaDon(IQueryable<HoaDon> query, int? branchId, IReadOnlyCollection<int>? allowedBranchIds)
        {
            if (branchId.HasValue)
            {
                query = query.Where(h => h.HopDong.PhongTro.ChiNhanhId == branchId.Value);
            }

            if (allowedBranchIds != null)
            {
                query = query.Where(h => allowedBranchIds.Contains(h.HopDong.PhongTro.ChiNhanhId));
            }

            return query;
        }

        private static IQueryable<YeuCauSuCo> LocSuCo(IQueryable<YeuCauSuCo> query, int? branchId, IReadOnlyCollection<int>? allowedBranchIds)
        {
            if (branchId.HasValue)
            {
                query = query.Where(s => s.PhongTro.ChiNhanhId == branchId.Value);
            }

            if (allowedBranchIds != null)
            {
                query = query.Where(s => allowedBranchIds.Contains(s.PhongTro.ChiNhanhId));
            }

            return query;
        }

        // HD1-HD3: đếm theo chi nhánh, không cần số tiền.
        private async Task<IReadOnlyList<DemHoaDonRow>> DemHoaDonTheoPhatHanhAsync(int? branchId, IReadOnlyCollection<int>? allowedBranchIds, IQueryable<HoaDon> goc, CancellationToken ct)
        {
            var query = LocHoaDon(goc.AsNoTracking(), branchId, allowedBranchIds);

            return await query
                .GroupBy(h => new { h.HopDong.PhongTro.ChiNhanhId, h.HopDong.PhongTro.ChiNhanh.TenChiNhanh })
                .Select(g => new DemHoaDonRow
                {
                    ChiNhanhId = g.Key.ChiNhanhId,
                    TenChiNhanh = g.Key.TenChiNhanh,
                    SoLuong = g.Count(),
                    NamCuNhat = g.Min(h => h.Nam)
                })
                .ToListAsync(ct);
        }

        // TT2, TT3: lấy từng hóa đơn rồi gom trong bộ nhớ để tránh GroupBy trên cột tính từ subquery ledger.
        private async Task<IReadOnlyList<DemHoaDonRow>> DemHoaDonKemConNoAsync(int? branchId, IReadOnlyCollection<int>? allowedBranchIds, IQueryable<HoaDon> goc, CancellationToken ct)
        {
            var query = LocHoaDon(goc.AsNoTracking(), branchId, allowedBranchIds);

            var rows = await query
                .Select(h => new
                {
                    h.HopDong.PhongTro.ChiNhanhId,
                    h.HopDong.PhongTro.ChiNhanh.TenChiNhanh,
                    h.Nam,
                    h.TongTien,
                    DaThu = h.LichSuThanhToans.Where(l => !l.IsDeleted).Sum(l => l.SoTienThanhToan)
                })
                .ToListAsync(ct);

            return rows
                .GroupBy(r => new { r.ChiNhanhId, r.TenChiNhanh })
                .Select(g => new DemHoaDonRow
                {
                    ChiNhanhId = g.Key.ChiNhanhId,
                    TenChiNhanh = g.Key.TenChiNhanh,
                    SoLuong = g.Count(),
                    NamCuNhat = g.Min(r => r.Nam),
                    TongConNo = g.Sum(r => r.TongTien - r.DaThu)
                })
                .ToList();
        }
    }
}
