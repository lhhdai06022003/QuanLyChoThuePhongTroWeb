using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.Services;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Persistence;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Features
{
    public class AutoInvoiceStore : IAutoInvoiceStore
    {
        private readonly ApplicationDbContext _context;

        public AutoInvoiceStore(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<AutoInvoiceRoomContext?> GetRoomContextAsync(int phongTroId, int thang, int nam, int soKyTrungBinh, CancellationToken ct = default)
        {
            var room = await _context.PhongTros
                .AsNoTracking()
                .Where(p => p.PhongTroId == phongTroId && !p.IsDeleted)
                .Select(p => new
                {
                    p.PhongTroId,
                    p.SoPhong,
                    p.ChiNhanhId,
                    p.ChiNhanh.TenChiNhanh,
                    SoDienThoai = p.ChiNhanh.SoDienThoai
                })
                .FirstOrDefaultAsync(ct);
            if (room == null)
            {
                return null;
            }

            var period = await _context.DichVuDienNuocCuaPhongs
                .AsNoTracking()
                .Where(p => p.PhongTroId == phongTroId && p.Thang == thang && p.Nam == nam && !p.IsDeleted)
                .Select(p => new
                {
                    p.DichVuDienNuocCuaPhongId,
                    p.TrangThaiGhiNhan,
                    p.ChiSoDienCu,
                    p.ChiSoDienMoi,
                    p.ChiSoNuocCu,
                    p.ChiSoNuocMoi,
                    p.DonGiaDien,
                    p.DonGiaNuoc
                })
                .FirstOrDefaultAsync(ct);

            var images = new List<AutoInvoiceMeterImage>();
            if (period != null)
            {
                images = await _context.AnhChiSoDongHos
                    .AsNoTracking()
                    .Where(a => a.DichVuDienNuocCuaPhongId == period.DichVuDienNuocCuaPhongId && !a.IsDeleted)
                    .OrderByDescending(a => a.NgayGui)
                    .ThenByDescending(a => a.AnhChiSoDongHoId)
                    .Select(a => new AutoInvoiceMeterImage(
                        a.AnhChiSoDongHoId,
                        a.LoaiDongHo,
                        a.TrangThaiXuLy,
                        a.GiaTriAIGoiY,
                        a.DoTinCay,
                        a.GiaTriXacNhan,
                        a.DuocChonLamChiSoChinhThuc,
                        a.NgayGui))
                    .ToListAsync(ct);
            }

            var consumptionRows = await _context.DichVuDienNuocCuaPhongs
                .AsNoTracking()
                .Where(p => p.PhongTroId == phongTroId
                            && !p.IsDeleted
                            && p.TrangThaiGhiNhan == TrangThaiGhiNhan.DaDuyet
                            && (p.Nam < nam || (p.Nam == nam && p.Thang < thang)))
                .OrderByDescending(p => p.Nam)
                .ThenByDescending(p => p.Thang)
                .Take(Math.Max(soKyTrungBinh, 0))
                .Select(p => new
                {
                    Dien = p.ChiSoDienMoi - p.ChiSoDienCu,
                    Nuoc = p.ChiSoNuocMoi - p.ChiSoNuocCu
                })
                .ToListAsync(ct);
            var consumptions = consumptionRows.Select(x => new AutoInvoiceConsumption(x.Dien, x.Nuoc)).ToList();

            // Mốc tháng giờ Việt Nam đổi sang UTC, khớp InvoiceIssuanceService.CreateDraftsAsync (cuối tháng = 23:59:59 ngày cuối).
            var (startOfMonthUtc, nextMonthStartUtc) = MeterPeriodPolicy.MonthRangeUtc(new MeterPeriod(thang, nam));
            var endOfMonthUtc = nextMonthStartUtc.AddSeconds(-1);
            var minEndUtc = startOfMonthUtc.AddDays(1);

            var contracts = await _context.HopDongs
                .AsNoTracking()
                .Where(h => h.PhongTroId == phongTroId &&
                            h.PhongTro.ChiNhanhId == room.ChiNhanhId &&
                            !h.IsDeleted &&
                            h.TrangThaiHopDong != TrangThaiHopDong.DaHuy &&
                            h.ThoiDiemBatDau <= endOfMonthUtc &&
                            (h.ThoiDiemKetThuc == null || h.ThoiDiemKetThuc >= minEndUtc))
                .OrderBy(h => h.HopDongId)
                .Select(h => new AutoInvoiceContractRef(h.HopDongId, h.NguoiThueId))
                .ToListAsync(ct);

            var contractIds = contracts.Select(c => c.HopDongId).ToList();
            var hasActiveInvoice = contractIds.Count > 0 && await _context.HoaDons
                .AsNoTracking()
                .AnyAsync(x => contractIds.Contains(x.HopDongId) && x.Thang == thang && x.Nam == nam && !x.IsDeleted, ct);

            var previous = MeterPeriodPolicy.Previous(new MeterPeriod(thang, nam));
            var (prevStartUtc, prevEndExclusiveUtc) = MeterPeriodPolicy.MonthRangeUtc(previous);
            var hadPreviousContract = await _context.HopDongs
                .AsNoTracking()
                .AnyAsync(h => h.PhongTroId == phongTroId &&
                               !h.IsDeleted &&
                               h.TrangThaiHopDong != TrangThaiHopDong.DaHuy &&
                               h.ThoiDiemBatDau < prevEndExclusiveUtc &&
                               (h.ThoiDiemKetThuc == null || h.ThoiDiemKetThuc.Value >= prevStartUtc), ct);
            var previousChuaChot = false;
            if (hadPreviousContract)
            {
                var previousStatus = await _context.DichVuDienNuocCuaPhongs
                    .AsNoTracking()
                    .Where(p => p.PhongTroId == phongTroId && p.Thang == previous.Thang && p.Nam == previous.Nam && !p.IsDeleted)
                    .Select(p => (TrangThaiGhiNhan?)p.TrangThaiGhiNhan)
                    .FirstOrDefaultAsync(ct);
                previousChuaChot = previousStatus != TrangThaiGhiNhan.DaDuyet;
            }

            int? nguoiDungKhachId = null;
            string? emailKhach = null;
            if (contracts.Count == 1)
            {
                var nguoiThueId = contracts[0].NguoiThueId;
                var rawEmail = await _context.NguoiThues
                    .AsNoTracking()
                    .Where(n => n.NguoiThueId == nguoiThueId)
                    .Select(n => n.Email)
                    .FirstOrDefaultAsync(ct);
                emailKhach = string.IsNullOrWhiteSpace(rawEmail) ? null : rawEmail.Trim();

                nguoiDungKhachId = await _context.NguoiDungs
                    .AsNoTracking()
                    .Where(u => u.NguoiThueId == nguoiThueId && !u.IsDeleted && u.IsActive)
                    .OrderBy(u => u.NguoiDungId)
                    .Select(u => (int?)u.NguoiDungId)
                    .FirstOrDefaultAsync(ct);
            }

            return new AutoInvoiceRoomContext
            {
                PhongTroId = room.PhongTroId,
                SoPhong = room.SoPhong,
                ChiNhanhId = room.ChiNhanhId,
                TenChiNhanh = room.TenChiNhanh ?? string.Empty,
                SoDienThoaiChiNhanh = room.SoDienThoai ?? string.Empty,
                Thang = thang,
                Nam = nam,
                DichVuDienNuocCuaPhongId = period?.DichVuDienNuocCuaPhongId,
                TrangThaiGhiNhan = period?.TrangThaiGhiNhan,
                ChiSoDienCu = period?.ChiSoDienCu ?? 0m,
                ChiSoDienMoi = period?.ChiSoDienMoi ?? 0m,
                ChiSoNuocCu = period?.ChiSoNuocCu ?? 0m,
                ChiSoNuocMoi = period?.ChiSoNuocMoi ?? 0m,
                DonGiaDien = period?.DonGiaDien ?? 0m,
                DonGiaNuoc = period?.DonGiaNuoc ?? 0m,
                Images = images,
                TieuThuCacKyTruoc = consumptions,
                HopDongHieuLuc = contracts,
                CoHoaDonHoatDong = hasActiveInvoice,
                KyTruocChuaChot = previousChuaChot,
                NguoiDungKhachId = nguoiDungKhachId,
                EmailKhach = emailKhach
            };
        }

        public async Task<int?> FindFallbackActorAsync(int chiNhanhId, CancellationToken ct = default)
        {
            return await _context.NhanVienChiNhanhs
                .AsNoTracking()
                .Where(n => n.ChiNhanhId == chiNhanhId
                            && n.IsActive
                            && n.NgayThuHoi == null
                            && n.NguoiDung.Role == Role.NhanVien
                            && n.NguoiDung.IsActive
                            && !n.NguoiDung.IsDeleted)
                .OrderBy(n => n.NgayPhanCong)
                .ThenBy(n => n.NhanVienChiNhanhId)
                .Select(n => (int?)n.NguoiDungId)
                .FirstOrDefaultAsync(ct);
        }

        public async Task<IReadOnlyList<AutoInvoiceDueItem>> GetDueProvisionalInvoicesAsync(DateTime nowUtc, CancellationToken ct = default)
        {
            return await _context.HoaDons
                .AsNoTracking()
                .Where(h => h.HanPhanHoi != null
                            && !h.IsDeleted
                            && h.TrangThaiPhatHanh == TrangThaiPhatHanhHoaDon.ChoDuyet
                            && h.HanPhanHoi <= nowUtc)
                .OrderBy(h => h.HanPhanHoi)
                .ThenBy(h => h.HoaDonId)
                .Select(h => new AutoInvoiceDueItem(h.HoaDonId, h.MaHoaDon, h.HopDong.PhongTroId, h.HanPhanHoi!.Value))
                .ToListAsync(ct);
        }

        public async Task<int?> GetLastProvisionalSubmitterIdAsync(int hoaDonId, CancellationToken ct = default)
        {
            return await _context.LichSuTrangThaiHoaDons
                .AsNoTracking()
                .Where(l => l.HoaDonId == hoaDonId
                            && l.TrangThaiPhatHanhMoi == TrangThaiPhatHanhHoaDon.ChoDuyet
                            && l.TrangThaiPhatHanhCu != TrangThaiPhatHanhHoaDon.ChoDuyet)
                .OrderByDescending(l => l.NgayThucHien)
                .ThenByDescending(l => l.LichSuTrangThaiHoaDonId)
                .Select(l => l.NguoiThucHienId)
                .FirstOrDefaultAsync(ct);
        }

        public async Task<AutoInvoiceAdjustmentSnapshot?> GetAdjustmentSnapshotAsync(int hoaDonId, CancellationToken ct = default)
        {
            return await _context.HoaDons
                .AsNoTracking()
                .Where(h => h.HoaDonId == hoaDonId && !h.IsDeleted)
                .Select(h => new AutoInvoiceAdjustmentSnapshot
                {
                    TongTien = h.TongTien,
                    DonGiaDien = h.DichVuDienNuocCuaPhong != null ? h.DichVuDienNuocCuaPhong.DonGiaDien : 0m,
                    DonGiaNuoc = h.DichVuDienNuocCuaPhong != null ? h.DichVuDienNuocCuaPhong.DonGiaNuoc : 0m,
                    TienThuePhong = h.HopDong.TienThuePhong,
                    HopDongBatDau = h.HopDong.ThoiDiemBatDau,
                    HopDongKetThuc = h.HopDong.ThoiDiemKetThuc,
                    ChiTiets = h.ChiTietHoaDonDichVus
                        .Where(c => !c.IsDeleted)
                        .OrderBy(c => c.ChiTietHoaDonId)
                        .Select(c => new AutoInvoiceInvoiceLine(
                            c.ChiTietHoaDonId,
                            c.TenDichVu,
                            c.DonGia,
                            c.SoLuong,
                            c.TongTien,
                            c.DichVuId,
                            c.DichVu != null ? (LoaiDichVu?)c.DichVu.LoaiDichVu : null,
                            c.DichVu != null ? c.DichVu.DonVi : null))
                        .ToList(),
                    HoaDonId = h.HoaDonId,
                    MaHoaDon = h.MaHoaDon,
                    HopDongId = h.HopDongId,
                    ChiNhanhId = h.HopDong.PhongTro.ChiNhanhId,
                    PhongTroId = h.HopDong.PhongTroId,
                    Thang = h.Thang,
                    Nam = h.Nam,
                    TrangThaiPhatHanh = h.TrangThaiPhatHanh,
                    TrangThaiHoaDon = h.TrangThaiHoaDon,
                    HanPhanHoi = h.HanPhanHoi,
                    DichVuDienNuocCuaPhongId = h.DichVuDienNuocCuaPhongId,
                    ChiSoDienCu = h.DichVuDienNuocCuaPhong != null ? h.DichVuDienNuocCuaPhong.ChiSoDienCu : 0m,
                    ChiSoDienMoi = h.DichVuDienNuocCuaPhong != null ? h.DichVuDienNuocCuaPhong.ChiSoDienMoi : 0m,
                    ChiSoNuocCu = h.DichVuDienNuocCuaPhong != null ? h.DichVuDienNuocCuaPhong.ChiSoNuocCu : 0m,
                    ChiSoNuocMoi = h.DichVuDienNuocCuaPhong != null ? h.DichVuDienNuocCuaPhong.ChiSoNuocMoi : 0m
                })
                .FirstOrDefaultAsync(ct);
        }

        public void DiscardPendingChanges()
        {
            _context.ChangeTracker.Clear();
        }
    }
}
