using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.Services;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Persistence;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Features
{
    public class AutoInvoiceStore : IAutoInvoiceStore
    {
        private readonly ApplicationDbContext _context;
        private readonly IMeterConsumptionHistoryStore _lichSuStore;

        public AutoInvoiceStore(ApplicationDbContext context, IMeterConsumptionHistoryStore lichSuStore)
        {
            _context = context;
            _lichSuStore = lichSuStore;
        }

        public async Task<AutoInvoiceRoomContext?> GetRoomContextAsync(int phongTroId, int thang, int nam, int soKyLichSuToiDa, CancellationToken ct = default)
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

            var lichSu = await _lichSuStore.GetAsync(phongTroId, thang, nam, soKyLichSuToiDa, ct);

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
                LichSu = lichSu,
                HopDongHieuLuc = contracts,
                CoHoaDonHoatDong = hasActiveInvoice,
                KyTruocChuaChot = previousChuaChot
            };
        }

        // Cùng kết quả với gọi GetRoomContextAsync cho từng khóa, nhưng 5 truy vấn cố định: phòng, kỳ điện nước (gồm kỳ trước
        // và lịch sử), ảnh, hợp đồng, hóa đơn. Lọc theo tháng ở bộ nhớ bằng đúng điều kiện của bản đọc từng phòng.
        public async Task<IReadOnlyDictionary<RoomPeriodKey, AutoInvoiceRoomContext>> GetRoomContextsAsync(
            IReadOnlyCollection<RoomPeriodKey> keys, int soKyLichSuToiDa, CancellationToken ct = default)
        {
            var result = new Dictionary<RoomPeriodKey, AutoInvoiceRoomContext>();
            var distinctKeys = keys.Distinct().ToList();
            if (distinctKeys.Count == 0)
            {
                return result;
            }

            var requestedRoomIds = distinctKeys.Select(k => k.PhongTroId).Distinct().ToList();
            var rooms = await _context.PhongTros
                .AsNoTracking()
                .Where(p => requestedRoomIds.Contains(p.PhongTroId) && !p.IsDeleted)
                .Select(p => new
                {
                    p.PhongTroId,
                    p.SoPhong,
                    p.ChiNhanhId,
                    p.ChiNhanh.TenChiNhanh,
                    SoDienThoai = p.ChiNhanh.SoDienThoai
                })
                .ToListAsync(ct);
            if (rooms.Count == 0)
            {
                return result;
            }

            var roomById = rooms.ToDictionary(r => r.PhongTroId);
            var roomIds = roomById.Keys.ToList();
            var maxKy = distinctKeys.Max(k => k.Nam * 12 + k.Thang);

            // Mọi kỳ chưa xóa tới kỳ lớn nhất được hỏi: đủ cho kỳ của khóa, kỳ trước và lịch sử tiêu thụ (chỉ mục duy nhất
            // (phòng, tháng, năm) trên bản ghi chưa xóa nên mỗi tháng của phòng có tối đa một kỳ).
            var periods = await _context.DichVuDienNuocCuaPhongs
                .AsNoTracking()
                .Where(p => roomIds.Contains(p.PhongTroId) && !p.IsDeleted && p.Nam * 12 + p.Thang <= maxKy)
                .Select(p => new
                {
                    p.PhongTroId,
                    p.Thang,
                    p.Nam,
                    p.DichVuDienNuocCuaPhongId,
                    p.TrangThaiGhiNhan,
                    p.ChiSoDienCu,
                    p.ChiSoDienMoi,
                    p.ChiSoNuocCu,
                    p.ChiSoNuocMoi,
                    p.DonGiaDien,
                    p.DonGiaNuoc
                })
                .ToListAsync(ct);
            var periodsByRoom = periods.ToLookup(p => p.PhongTroId);

            var keyPeriodIds = distinctKeys
                .Select(k => periodsByRoom[k.PhongTroId].FirstOrDefault(p => p.Thang == k.Thang && p.Nam == k.Nam))
                .Where(p => p != null)
                .Select(p => p!.DichVuDienNuocCuaPhongId)
                .Distinct()
                .ToList();
            var images = keyPeriodIds.Count == 0
                ? new List<(int PeriodId, AutoInvoiceMeterImage Image)>()
                : (await _context.AnhChiSoDongHos
                    .AsNoTracking()
                    .Where(a => keyPeriodIds.Contains(a.DichVuDienNuocCuaPhongId) && !a.IsDeleted)
                    .OrderByDescending(a => a.NgayGui)
                    .ThenByDescending(a => a.AnhChiSoDongHoId)
                    .Select(a => new
                    {
                        a.DichVuDienNuocCuaPhongId,
                        Image = new AutoInvoiceMeterImage(
                            a.AnhChiSoDongHoId,
                            a.LoaiDongHo,
                            a.TrangThaiXuLy,
                            a.GiaTriAIGoiY,
                            a.DoTinCay,
                            a.GiaTriXacNhan,
                            a.DuocChonLamChiSoChinhThuc,
                            a.NgayGui)
                    })
                    .ToListAsync(ct))
                    .Select(x => (x.DichVuDienNuocCuaPhongId, x.Image))
                    .ToList();
            var imagesByPeriod = images.ToLookup(i => i.Item1, i => i.Item2);

            var contracts = await _context.HopDongs
                .AsNoTracking()
                .Where(h => roomIds.Contains(h.PhongTroId) && !h.IsDeleted && h.TrangThaiHopDong != TrangThaiHopDong.DaHuy)
                .Select(h => new
                {
                    h.HopDongId,
                    h.NguoiThueId,
                    h.PhongTroId,
                    ChiNhanhId = h.PhongTro.ChiNhanhId,
                    h.ThoiDiemBatDau,
                    h.ThoiDiemKetThuc
                })
                .ToListAsync(ct);
            var contractsByRoom = contracts.ToLookup(c => c.PhongTroId);

            var keyCodes = distinctKeys.Select(k => k.Nam * 12 + k.Thang).Distinct().ToList();
            var contractIds = contracts.Select(c => c.HopDongId).ToList();
            var invoices = contractIds.Count == 0
                ? new List<(int HopDongId, int Thang, int Nam)>()
                : (await _context.HoaDons
                    .AsNoTracking()
                    .Where(x => contractIds.Contains(x.HopDongId) && !x.IsDeleted && keyCodes.Contains(x.Nam * 12 + x.Thang))
                    .Select(x => new { x.HopDongId, x.Thang, x.Nam })
                    .ToListAsync(ct))
                    .Select(x => (x.HopDongId, x.Thang, x.Nam))
                    .ToList();
            var invoiceSet = invoices.ToHashSet();

            foreach (var key in distinctKeys)
            {
                if (!roomById.TryGetValue(key.PhongTroId, out var room))
                {
                    continue;
                }

                var roomPeriods = periodsByRoom[key.PhongTroId];
                var period = roomPeriods.FirstOrDefault(p => p.Thang == key.Thang && p.Nam == key.Nam);

                var (startOfMonthUtc, nextMonthStartUtc) = MeterPeriodPolicy.MonthRangeUtc(new MeterPeriod(key.Thang, key.Nam));
                var endOfMonthUtc = nextMonthStartUtc.AddSeconds(-1);
                var minEndUtc = startOfMonthUtc.AddDays(1);

                var inMonth = contractsByRoom[key.PhongTroId]
                    .Where(h => h.ThoiDiemBatDau <= endOfMonthUtc && (h.ThoiDiemKetThuc == null || h.ThoiDiemKetThuc >= minEndUtc))
                    .OrderBy(h => h.HopDongId)
                    .ToList();
                var hieuLuc = inMonth
                    .Where(h => h.ChiNhanhId == room.ChiNhanhId)
                    .Select(h => new AutoInvoiceContractRef(h.HopDongId, h.NguoiThueId))
                    .ToList();

                var historyContracts = inMonth
                    .Take(2)
                    .Select(h => new MeterConsumptionHistoryStore.HistoryContract(h.HopDongId, h.ThoiDiemBatDau, h.ThoiDiemKetThuc))
                    .ToList();
                var historyRows = historyContracts.Count == 1 && soKyLichSuToiDa > 0
                    ? roomPeriods
                        .Where(p => p.TrangThaiGhiNhan == TrangThaiGhiNhan.DaDuyet && (p.Nam < key.Nam || (p.Nam == key.Nam && p.Thang < key.Thang)))
                        .OrderByDescending(p => p.Nam)
                        .ThenByDescending(p => p.Thang)
                        .Take(MeterConsumptionHistoryStore.SoKyLayToiDa)
                        .Select(p => new MeterConsumptionHistoryStore.HistoryRow(p.Thang, p.Nam, p.ChiSoDienMoi - p.ChiSoDienCu, p.ChiSoNuocMoi - p.ChiSoNuocCu))
                        .ToList()
                    : new List<MeterConsumptionHistoryStore.HistoryRow>();
                var lichSu = MeterConsumptionHistoryStore.Build(key.Thang, key.Nam, soKyLichSuToiDa, historyContracts, historyRows);

                var hasActiveInvoice = hieuLuc.Any(c => invoiceSet.Contains((c.HopDongId, key.Thang, key.Nam)));

                var previous = MeterPeriodPolicy.Previous(new MeterPeriod(key.Thang, key.Nam));
                var (prevStartUtc, prevEndExclusiveUtc) = MeterPeriodPolicy.MonthRangeUtc(previous);
                var hadPreviousContract = contractsByRoom[key.PhongTroId]
                    .Any(h => h.ThoiDiemBatDau < prevEndExclusiveUtc && (h.ThoiDiemKetThuc == null || h.ThoiDiemKetThuc.Value >= prevStartUtc));
                var previousChuaChot = false;
                if (hadPreviousContract)
                {
                    var previousPeriod = roomPeriods.FirstOrDefault(p => p.Thang == previous.Thang && p.Nam == previous.Nam);
                    previousChuaChot = previousPeriod?.TrangThaiGhiNhan != TrangThaiGhiNhan.DaDuyet;
                }

                result[key] = new AutoInvoiceRoomContext
                {
                    PhongTroId = room.PhongTroId,
                    SoPhong = room.SoPhong,
                    ChiNhanhId = room.ChiNhanhId,
                    TenChiNhanh = room.TenChiNhanh ?? string.Empty,
                    SoDienThoaiChiNhanh = room.SoDienThoai ?? string.Empty,
                    Thang = key.Thang,
                    Nam = key.Nam,
                    DichVuDienNuocCuaPhongId = period?.DichVuDienNuocCuaPhongId,
                    TrangThaiGhiNhan = period?.TrangThaiGhiNhan,
                    ChiSoDienCu = period?.ChiSoDienCu ?? 0m,
                    ChiSoDienMoi = period?.ChiSoDienMoi ?? 0m,
                    ChiSoNuocCu = period?.ChiSoNuocCu ?? 0m,
                    ChiSoNuocMoi = period?.ChiSoNuocMoi ?? 0m,
                    DonGiaDien = period?.DonGiaDien ?? 0m,
                    DonGiaNuoc = period?.DonGiaNuoc ?? 0m,
                    Images = period == null
                        ? new List<AutoInvoiceMeterImage>()
                        : imagesByPeriod[period.DichVuDienNuocCuaPhongId].ToList(),
                    LichSu = lichSu,
                    HopDongHieuLuc = hieuLuc,
                    CoHoaDonHoatDong = hasActiveInvoice,
                    KyTruocChuaChot = previousChuaChot
                };
            }

            return result;
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
