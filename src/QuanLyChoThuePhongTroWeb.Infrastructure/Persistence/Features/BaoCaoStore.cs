using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using QuanLyChoThuePhongTroWeb.Application.Features.BaoCaos.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Features.Reservations;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Features
{
    // Store đọc cho báo cáo thống kê: chỉ trả row, việc gom số liệu nằm ở các builder của Application.
    public class BaoCaoStore : IBaoCaoStore
    {
        private readonly ApplicationDbContext _context;

        public BaoCaoStore(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<string?> GetTenChiNhanhAsync(int chiNhanhId, CancellationToken cancellationToken = default)
        {
            return await _context.ChiNhanhs
                .AsNoTracking()
                .Where(c => c.ChiNhanhId == chiNhanhId && !c.IsDeleted)
                .Select(c => c.TenChiNhanh)
                .FirstOrDefaultAsync(cancellationToken);
        }

        // Hóa đơn đã gửi, chưa xóa, kỳ nằm trong khoảng; lọc chi nhánh qua phòng của hợp đồng (giống DashboardStore).
        private IQueryable<HoaDon> HoaDonDaGui(int? chiNhanhId, IReadOnlyCollection<int>? allowedBranchIds, int tuKyKey, int denKyKey)
        {
            var query = _context.HoaDons
                .AsNoTracking()
                .Where(h => !h.IsDeleted
                            && h.TrangThaiPhatHanh == TrangThaiPhatHanhHoaDon.DaGui
                            && h.Nam * 100 + h.Thang >= tuKyKey
                            && h.Nam * 100 + h.Thang <= denKyKey);

            if (chiNhanhId.HasValue)
            {
                query = query.Where(h => h.HopDong.PhongTro.ChiNhanhId == chiNhanhId.Value);
            }

            if (allowedBranchIds != null)
            {
                query = query.Where(h => allowedBranchIds.Contains(h.HopDong.PhongTro.ChiNhanhId));
            }

            return query;
        }

        public async Task<IReadOnlyList<BaoCaoHoaDonRow>> GetHoaDonDaGuiAsync(int? chiNhanhId, IReadOnlyCollection<int>? allowedBranchIds, int tuKyKey, int denKyKey, CancellationToken cancellationToken = default)
        {
            return await HoaDonDaGui(chiNhanhId, allowedBranchIds, tuKyKey, denKyKey)
                .Select(h => new BaoCaoHoaDonRow(
                    h.HoaDonId,
                    h.Thang,
                    h.Nam,
                    h.HopDong.PhongTro.ChiNhanhId,
                    h.HopDong.PhongTro.ChiNhanh.TenChiNhanh,
                    h.TongTien))
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<BaoCaoThanhToanRow>> GetThanhToanCuaHoaDonDaGuiAsync(int? chiNhanhId, IReadOnlyCollection<int>? allowedBranchIds, int tuKyKey, int denKyKey, CancellationToken cancellationToken = default)
        {
            return await HoaDonDaGui(chiNhanhId, allowedBranchIds, tuKyKey, denKyKey)
                .SelectMany(h => h.LichSuThanhToans.Where(l => !l.IsDeleted))
                .Select(l => new BaoCaoThanhToanRow(
                    l.HoaDonId,
                    l.SoTienThanhToan,
                    l.PhuongThucThanhToan,
                    l.MaGiaoDich,
                    l.GhiChu))
                .ToListAsync(cancellationToken);
        }

        public async Task<decimal> GetTienCocDangGiuAsync(int? chiNhanhId, IReadOnlyCollection<int>? allowedBranchIds, CancellationToken cancellationToken = default)
        {
            var query = _context.HopDongs
                .AsNoTracking()
                .Where(h => !h.IsDeleted && h.TrangThaiHopDong == TrangThaiHopDong.DangHoatDong);

            if (chiNhanhId.HasValue)
            {
                query = query.Where(h => h.PhongTro.ChiNhanhId == chiNhanhId.Value);
            }

            if (allowedBranchIds != null)
            {
                query = query.Where(h => allowedBranchIds.Contains(h.PhongTro.ChiNhanhId));
            }

            return await query.SumAsync(h => (decimal?)h.TienCocPhong, cancellationToken) ?? 0m;
        }

        public async Task<IReadOnlyList<BaoCaoGiuChoRow>> GetGiuChoDaNhanTienAsync(int? chiNhanhId, IReadOnlyCollection<int>? allowedBranchIds, CancellationToken cancellationToken = default)
        {
            var query = _context.YeuCauGiuChos
                .AsNoTracking()
                .Where(y => y.YeuCauThanhToanGiuChos.Any(p => p.GiaoDichGiuChos.Any()));

            if (chiNhanhId.HasValue)
            {
                query = query.Where(y => y.PhongTro.ChiNhanhId == chiNhanhId.Value);
            }

            if (allowedBranchIds != null)
            {
                query = query.Where(y => allowedBranchIds.Contains(y.PhongTro.ChiNhanhId));
            }

            var rows = await query
                .Select(y => new
                {
                    y.YeuCauGiuChoId,
                    ChiNhanhId = y.PhongTro.ChiNhanhId,
                    TenChiNhanh = y.PhongTro.ChiNhanh.TenChiNhanh,
                    y.TrangThai,
                    DaNhan = y.YeuCauThanhToanGiuChos.SelectMany(p => p.GiaoDichGiuChos).Sum(g => (decimal?)g.SoTienThucNhan) ?? 0m,
                    DaApDung = y.ApDungTienGiuChoVaoTienCoc != null ? y.ApDungTienGiuChoVaoTienCoc.SoTienApDung : 0m,
                    CocGiuLai = y.TrangThai == TrangThaiYeuCauGiuCho.KhachKhongThue ? y.SoTienGiuCho ?? 0m : 0m,
                    NgayGiuLai = y.LichSuTrangThais.Where(h => h.TrangThaiSau == TrangThaiYeuCauGiuCho.KhachKhongThue)
                        .Select(h => (DateTime?)h.NgayThucHien).Min(),
                    DangGiuChoTuPhucVu = y.TrangThai == TrangThaiYeuCauGiuCho.DangGiuCho &&
                        y.LichSuTrangThais.Any(h => h.LyDo == SelfServiceReservationRules.CreationReason),
                    CoQuyetDinh = y.QuyetDinhHoanTienGiuCho != null,
                    TrangThaiQuyetDinh = y.QuyetDinhHoanTienGiuCho != null ? (TrangThaiHoanTien?)y.QuyetDinhHoanTienGiuCho.TrangThai : null,
                    NgayQuyetDinh = y.QuyetDinhHoanTienGiuCho != null ? (DateTime?)y.QuyetDinhHoanTienGiuCho.NgayQuyetDinh : null,
                    DaHoan = y.QuyetDinhHoanTienGiuCho != null
                        ? (y.QuyetDinhHoanTienGiuCho.GiaoDichHoanTiens.Sum(h => (decimal?)h.SoTienHoan) ?? 0m)
                        : 0m
                })
                .ToListAsync(cancellationToken);

            return rows
                .Where(r => r.DaNhan > 0m)
                .Select(r => new BaoCaoGiuChoRow(
                    r.YeuCauGiuChoId,
                    r.ChiNhanhId,
                    r.TenChiNhanh,
                    r.TrangThai,
                    r.DaNhan,
                    r.DaApDung,
                    r.CoQuyetDinh,
                    r.TrangThaiQuyetDinh,
                    r.NgayQuyetDinh,
                    r.DaHoan,
                    r.CocGiuLai,
                    r.NgayGiuLai,
                    r.DangGiuChoTuPhucVu))
                .ToList();
        }

        public async Task<IReadOnlyList<BaoCaoHoaDonNoRow>> GetHoaDonConNoAsync(int? chiNhanhId, IReadOnlyCollection<int>? allowedBranchIds, CancellationToken cancellationToken = default)
        {
            var query = _context.HoaDons.AsNoTracking().DaGuiChuaThuDu();

            if (chiNhanhId.HasValue)
            {
                query = query.Where(h => h.HopDong.PhongTro.ChiNhanhId == chiNhanhId.Value);
            }

            if (allowedBranchIds != null)
            {
                query = query.Where(h => allowedBranchIds.Contains(h.HopDong.PhongTro.ChiNhanhId));
            }

            var rows = await query
                .Select(h => new
                {
                    h.HoaDonId,
                    h.Thang,
                    h.Nam,
                    h.HanThanhToan,
                    h.TongTien,
                    DaThu = h.LichSuThanhToans.Where(l => !l.IsDeleted).Sum(l => l.SoTienThanhToan),
                    h.HopDong.NguoiThueId,
                    h.HopDong.NguoiThue.HoVaTen,
                    h.HopDong.PhongTro.SoPhong,
                    ChiNhanhId = h.HopDong.PhongTro.ChiNhanhId,
                    TenChiNhanh = h.HopDong.PhongTro.ChiNhanh.TenChiNhanh,
                    // Cùng biểu thức với bộ lọc hopDongDaKetThucConNo của InvoiceViewStore.
                    DaTraPhong = h.HopDong.TrangThaiHopDong == TrangThaiHopDong.DaKetThuc
                        && !h.HopDong.IsDeleted
                        && !_context.HopDongs.Any(o => !o.IsDeleted
                            && o.TrangThaiHopDong == TrangThaiHopDong.DangHoatDong
                            && o.PhongTroId == h.HopDong.PhongTroId
                            && o.NguoiThueId == h.HopDong.NguoiThueId)
                })
                .ToListAsync(cancellationToken);

            return rows
                .Where(r => r.TongTien - r.DaThu > 0m)
                .Select(r => new BaoCaoHoaDonNoRow(
                    r.HoaDonId,
                    r.Thang,
                    r.Nam,
                    r.HanThanhToan,
                    r.TongTien,
                    r.DaThu,
                    r.NguoiThueId,
                    r.HoVaTen,
                    r.SoPhong,
                    r.ChiNhanhId,
                    r.TenChiNhanh,
                    r.DaTraPhong))
                .ToList();
        }

        public async Task<IReadOnlyList<BaoCaoPhongRow>> GetPhongAsync(int? chiNhanhId, IReadOnlyCollection<int>? allowedBranchIds, CancellationToken cancellationToken = default)
        {
            var query = _context.PhongTros.AsNoTracking().Where(p => !p.IsDeleted);

            if (chiNhanhId.HasValue)
            {
                query = query.Where(p => p.ChiNhanhId == chiNhanhId.Value);
            }

            if (allowedBranchIds != null)
            {
                query = query.Where(p => allowedBranchIds.Contains(p.ChiNhanhId));
            }

            return await query
                .Select(p => new BaoCaoPhongRow(p.PhongTroId, p.SoPhong, p.ChiNhanhId, p.ChiNhanh.TenChiNhanh, p.NgayTao, p.TrangThai))
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<BaoCaoHopDongRow>> GetHopDongAsync(int? chiNhanhId, IReadOnlyCollection<int>? allowedBranchIds, CancellationToken cancellationToken = default)
        {
            var query = _context.HopDongs
                .AsNoTracking()
                .Where(h => !h.IsDeleted && h.TrangThaiHopDong != TrangThaiHopDong.DaHuy);

            if (chiNhanhId.HasValue)
            {
                query = query.Where(h => h.PhongTro.ChiNhanhId == chiNhanhId.Value);
            }

            if (allowedBranchIds != null)
            {
                query = query.Where(h => allowedBranchIds.Contains(h.PhongTro.ChiNhanhId));
            }

            return await query
                .Select(h => new BaoCaoHopDongRow(
                    h.HopDongId,
                    h.MaHopDong,
                    h.PhongTroId,
                    h.PhongTro.ChiNhanhId,
                    h.ThoiDiemBatDau,
                    h.ThoiDiemKetThuc,
                    h.TrangThaiHopDong))
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<BaoCaoDienNuocRow>> GetDienNuocDaDuyetAsync(int? chiNhanhId, IReadOnlyCollection<int>? allowedBranchIds, int tuKyKey, int denKyKey, CancellationToken cancellationToken = default)
        {
            var query = _context.DichVuDienNuocCuaPhongs
                .AsNoTracking()
                .Where(d => !d.IsDeleted
                            && d.TrangThaiGhiNhan == TrangThaiGhiNhan.DaDuyet
                            && d.Nam * 100 + d.Thang >= tuKyKey
                            && d.Nam * 100 + d.Thang <= denKyKey);

            if (chiNhanhId.HasValue)
            {
                query = query.Where(d => d.PhongTro.ChiNhanhId == chiNhanhId.Value);
            }

            if (allowedBranchIds != null)
            {
                query = query.Where(d => allowedBranchIds.Contains(d.PhongTro.ChiNhanhId));
            }

            return await query
                .Select(d => new BaoCaoDienNuocRow(
                    d.PhongTroId,
                    d.PhongTro.SoPhong,
                    d.PhongTro.ChiNhanhId,
                    d.PhongTro.ChiNhanh.TenChiNhanh,
                    d.Thang,
                    d.Nam,
                    d.ChiSoDienCu,
                    d.ChiSoDienMoi,
                    d.DonGiaDien,
                    d.ChiSoNuocCu,
                    d.ChiSoNuocMoi,
                    d.DonGiaNuoc))
                .ToListAsync(cancellationToken);
        }
    }
}
