using Microsoft.EntityFrameworkCore;
using QuanLyChoThuePhongTroWeb.Application.Features.GiuChos.Persistence;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using QuanLyChoThuePhongTroWeb.Domain.Rules;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Features;

public sealed class CanTruTienGiuChoStore(ApplicationDbContext context) : ICanTruTienGiuChoStore
{
    public Task<bool> HasAllocationAsync(int invoiceId) =>
        context.CanTruTienGiuChoHoaDons.AsNoTracking().AnyAsync(item => item.HoaDonId == invoiceId);

    public async Task<decimal> GetAvailableAsync(int contractId, int month, int year)
    {
        var application = await context.ApDungTienGiuChoVaoTienCocs.AsNoTracking()
            .Include(item => item.HopDong).FirstOrDefaultAsync(item => item.HopDongId == contractId);
        if (application is null) return 0;
        var start = application.HopDong.ThoiDiemBatDau.AddHours(7);
        if (year * 12 + month < start.Year * 12 + start.Month) return 0;
        var used = await context.CanTruTienGiuChoHoaDons.Where(item =>
            item.ApDungTienGiuChoVaoTienCocId == application.ApDungTienGiuChoVaoTienCocId &&
            !item.HoaDon.IsDeleted && item.HoaDon.TrangThaiPhatHanh != TrangThaiPhatHanhHoaDon.DaHuy)
            .SumAsync(item => item.SoTienCanTru);
        return Math.Max(0, application.SoTienApDung - used);
    }

    public async Task PrepareInvoicesAsync(IReadOnlyList<HoaDon> invoices)
    {
        if (context.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Cấn trừ cọc phải cùng transaction tạo hóa đơn.");
        foreach (var group in invoices.GroupBy(item => item.HopDongId).OrderBy(item => item.Key))
        {
            var application = await context.ApDungTienGiuChoVaoTienCocs.FromSqlInterpolated(
                $"SELECT * FROM ap_dung_tien_giu_cho_vao_tien_coc WHERE \"HopDongId\" = {group.Key} FOR UPDATE")
                .SingleOrDefaultAsync();
            if (application is null) continue;
            var startUtc = await context.HopDongs.Where(item => item.HopDongId == group.Key)
                .Select(item => item.ThoiDiemBatDau).SingleAsync();
            var startLocal = startUtc.AddHours(7);
            var startPeriod = startLocal.Year * 12 + startLocal.Month;
            var used = await context.CanTruTienGiuChoHoaDons
                .Where(item => item.ApDungTienGiuChoVaoTienCocId == application.ApDungTienGiuChoVaoTienCocId &&
                    !item.HoaDon.IsDeleted && item.HoaDon.TrangThaiPhatHanh != TrangThaiPhatHanhHoaDon.DaHuy)
                .SumAsync(item => item.SoTienCanTru);
            var remaining = application.SoTienApDung - used;
            foreach (var invoice in group.OrderBy(item => item.Nam).ThenBy(item => item.Thang))
            {
                var period = invoice.Nam * 12 + invoice.Thang;
                if (remaining <= 0 || period < startPeriod) continue;
                if (period > startPeriod && !await context.HoaDons.AnyAsync(item =>
                    item.HopDongId == group.Key && item.Thang == startLocal.Month && item.Nam == startLocal.Year &&
                    !item.IsDeleted && item.TrangThaiPhatHanh != TrangThaiPhatHanhHoaDon.DaHuy))
                    throw new InvalidOperationException("Hãy phát sinh hóa đơn tháng bắt đầu hợp đồng trước để cấn trừ cọc.");
                remaining -= GiuChoRules.CanTruVaoHoaDon(invoice,
                    application.ApDungTienGiuChoVaoTienCocId, remaining);
            }
        }
    }
}
