using Microsoft.EntityFrameworkCore;
using QuanLyChoThuePhongTroWeb.Application.Features.GiuChos.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.GiuChos.Persistence;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Features;

public sealed class TheoDoiGiuChoStore(ApplicationDbContext context) : ITheoDoiGiuChoStore
{
    public async Task<ReservationProgressDto?> GetMineAsync(int reservationId, int actorId)
    {
        var hold = await context.YeuCauGiuChos.AsNoTracking()
            .Include(item => item.PhongTro).ThenInclude(item => item.ChiNhanh)
            .Include(item => item.YeuCauThanhToanGiuChos).ThenInclude(item => item.GiaoDichGiuChos)
            .Include(item => item.ApDungTienGiuChoVaoTienCoc)
            .Include(item => item.LichSuTrangThais)
            .FirstOrDefaultAsync(item => item.YeuCauGiuChoId == reservationId &&
                item.KhachVangLai.NguoiDungId == actorId && item.KhachVangLai.NguoiDung.IsActive &&
                !item.KhachVangLai.NguoiDung.IsDeleted);
        if (hold is null) return null;
        var applied = hold.ApDungTienGiuChoVaoTienCoc;
        var credited = applied is null ? 0 : await context.CanTruTienGiuChoHoaDons
            .Where(item => item.ApDungTienGiuChoVaoTienCocId == applied.ApDungTienGiuChoVaoTienCocId &&
                !item.HoaDon.IsDeleted && item.HoaDon.TrangThaiPhatHanh != TrangThaiPhatHanhHoaDon.DaHuy)
            .SumAsync(item => item.SoTienCanTru);
        return new(hold.YeuCauGiuChoId, hold.PhongTro.SoPhong, hold.PhongTro.ChiNhanh.TenChiNhanh,
            hold.TrangThai.ToString(), hold.SoTienGiuCho,
            hold.YeuCauThanhToanGiuChos.SelectMany(item => item.GiaoDichGiuChos).Sum(item => item.SoTienThucNhan),
            hold.HanThanhToan, hold.HanKyHopDong, hold.LyDoTuChoiHoacHuy, applied?.HopDongId,
            applied?.SoTienApDung ?? 0, credited, Math.Max(0, (applied?.SoTienApDung ?? 0) - credited),
            hold.LichSuTrangThais.OrderBy(item => item.NgayThucHien)
                .Select(item => new ReservationTimelineItem(item.NgayThucHien, item.TrangThaiSau.ToString(), item.LyDo)).ToArray());
    }
}
