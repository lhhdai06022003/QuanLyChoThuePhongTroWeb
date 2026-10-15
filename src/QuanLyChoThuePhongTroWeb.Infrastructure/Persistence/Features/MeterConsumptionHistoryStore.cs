using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.Services;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Features
{
    public class MeterConsumptionHistoryStore : IMeterConsumptionHistoryStore
    {
        // Lấy dư kỳ gần nhất rồi lọc tháng trọn trong hợp đồng ở bộ nhớ.
        private const int SoKyLayToiDa = 24;

        private readonly ApplicationDbContext _context;

        public MeterConsumptionHistoryStore(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<MeterConsumptionHistory> GetAsync(int phongTroId, int thang, int nam, int soKyToiDa, CancellationToken ct = default)
        {
            var soNgayCuaThang = DateTime.DaysInMonth(nam, thang);

            // Mốc tháng giờ Việt Nam đổi sang UTC, cùng điều kiện hợp đồng với AutoInvoiceStore.GetRoomContextAsync.
            var (startOfMonthUtc, nextMonthStartUtc) = MeterPeriodPolicy.MonthRangeUtc(new MeterPeriod(thang, nam));
            var endOfMonthUtc = nextMonthStartUtc.AddSeconds(-1);
            var minEndUtc = startOfMonthUtc.AddDays(1);

            var contracts = await _context.HopDongs
                .AsNoTracking()
                .Where(h => h.PhongTroId == phongTroId &&
                            !h.IsDeleted &&
                            h.TrangThaiHopDong != TrangThaiHopDong.DaHuy &&
                            h.ThoiDiemBatDau <= endOfMonthUtc &&
                            (h.ThoiDiemKetThuc == null || h.ThoiDiemKetThuc >= minEndUtc))
                .OrderBy(h => h.HopDongId)
                .Select(h => new { h.HopDongId, h.ThoiDiemBatDau, h.ThoiDiemKetThuc })
                .Take(2)
                .ToListAsync(ct);

            if (contracts.Count != 1)
            {
                return new MeterConsumptionHistory(Array.Empty<MeterConsumption>(), soNgayCuaThang, soNgayCuaThang, null);
            }

            var contract = contracts[0];

            // Số ngày lịch Việt Nam hợp đồng phủ trong tháng, tính cả hai đầu, tối thiểu 1.
            var firstDay = new DateTime(nam, thang, 1);
            var lastDay = new DateTime(nam, thang, soNgayCuaThang);
            var from = contract.ThoiDiemBatDau.AddHours(7).Date;
            if (from < firstDay)
            {
                from = firstDay;
            }
            var to = lastDay;
            if (contract.ThoiDiemKetThuc.HasValue)
            {
                var endVn = contract.ThoiDiemKetThuc.Value.AddHours(7).Date;
                if (endVn < to)
                {
                    to = endVn;
                }
            }
            var soNgayPhu = Math.Max(1, (to - from).Days + 1);

            if (soKyToiDa <= 0)
            {
                return new MeterConsumptionHistory(Array.Empty<MeterConsumption>(), soNgayPhu, soNgayCuaThang, contract.HopDongId);
            }

            var rows = await _context.DichVuDienNuocCuaPhongs
                .AsNoTracking()
                .Where(p => p.PhongTroId == phongTroId
                            && !p.IsDeleted
                            && p.TrangThaiGhiNhan == TrangThaiGhiNhan.DaDuyet
                            && (p.Nam < nam || (p.Nam == nam && p.Thang < thang)))
                .OrderByDescending(p => p.Nam)
                .ThenByDescending(p => p.Thang)
                .Take(SoKyLayToiDa)
                .Select(p => new
                {
                    p.Thang,
                    p.Nam,
                    Dien = p.ChiSoDienMoi - p.ChiSoDienCu,
                    Nuoc = p.ChiSoNuocMoi - p.ChiSoNuocCu
                })
                .ToListAsync(ct);

            var history = new List<MeterConsumption>();
            foreach (var row in rows)
            {
                var (startUtc, nextStartUtc) = MeterPeriodPolicy.MonthRangeUtc(new MeterPeriod(row.Thang, row.Nam));
                var tronThang = contract.ThoiDiemBatDau <= startUtc
                                && (contract.ThoiDiemKetThuc == null || contract.ThoiDiemKetThuc >= nextStartUtc.AddSeconds(-1));
                if (!tronThang)
                {
                    continue;
                }
                history.Add(new MeterConsumption(row.Dien, row.Nuoc));
                if (history.Count >= soKyToiDa)
                {
                    break;
                }
            }

            return new MeterConsumptionHistory(history, soNgayPhu, soNgayCuaThang, contract.HopDongId);
        }
    }
}
