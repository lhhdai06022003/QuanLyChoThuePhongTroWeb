using System.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using QuanLyChoThuePhongTroWeb.Application.Features.GiuChos.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.GiuChos.Persistence;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using QuanLyChoThuePhongTroWeb.Domain.Rules;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Features;

public sealed class GiuChoStore(ApplicationDbContext context) : IGiuChoStore
{
    public async Task<int?> TryCreateAsync(string code, int? viewingId, int actorId)
    {
        try
        {
            await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var guest = await BookingCustomerProfile.GetOrCreateAsync(context, actorId);
            if (guest is null || string.IsNullOrWhiteSpace(guest.Email) ||
                string.IsNullOrWhiteSpace(guest.CCCD)) return null;

            var roomId = await PhongCongKhaiStore.FilterPublicRooms(
                    context.PhongTros.AsNoTracking(), context.YeuCauGiuChos.AsNoTracking())
                .Where(room => room.MaCongKhai == code)
                .Select(room => (int?)room.PhongTroId).FirstOrDefaultAsync();
            if (roomId is null) return null;

            if (viewingId is int sourceId && !await context.YeuCauXemPhongs.AnyAsync(viewing =>
                    viewing.YeuCauXemPhongId == sourceId && viewing.KhachVangLaiId == guest.KhachVangLaiId &&
                    viewing.PhongTroId == roomId &&
                    viewing.TrangThai == TrangThaiYeuCauXemPhong.DaXacNhanLich)) return null;

            var existing = await context.YeuCauGiuChos.AsNoTracking()
                .Where(hold => hold.KhachVangLaiId == guest.KhachVangLaiId && hold.PhongTroId == roomId &&
                    hold.TrangThai == TrangThaiYeuCauGiuCho.MoiTao)
                .Select(hold => (int?)hold.YeuCauGiuChoId).FirstOrDefaultAsync();
            if (existing is int existingId) return existingId;

            var hold = new YeuCauGiuCho
            {
                KhachVangLaiId = guest.KhachVangLaiId,
                PhongTroId = roomId.Value,
                YeuCauXemPhongId = viewingId,
                TrangThai = TrangThaiYeuCauGiuCho.MoiTao,
                NgayTao = DateTime.UtcNow
            };
            hold.LichSuTrangThais.Add(new LichSuTrangThaiYeuCauGiuCho
            {
                TrangThaiSau = TrangThaiYeuCauGiuCho.MoiTao,
                LoaiTacNhan = LoaiTacNhan.NguoiDung,
                NguoiThucHienId = actorId,
                NgayThucHien = hold.NgayTao
            });
            context.YeuCauGiuChos.Add(hold);
            await context.SaveChangesAsync();
            await transaction.CommitAsync();
            return hold.YeuCauGiuChoId;
        }
        catch (Exception ex) when (DatabaseConflict.IsExpected(ex)) { return null; }
    }

    public async Task<bool> TryApproveAsync(int id, DateTime deadlineUtc,
        DateTime contractDeadlineUtc, int actorId)
    {
        try
        {
            await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var actor = await context.NguoiDungs.AsNoTracking()
                .Where(user => user.NguoiDungId == actorId && user.IsActive && !user.IsDeleted)
                .Select(user => new { user.Role }).FirstOrDefaultAsync();
            if (actor is null || actor.Role is not (Role.Admin or Role.NhanVien)) return false;

            var hold = await context.YeuCauGiuChos.Include(item => item.PhongTro)
                .FirstOrDefaultAsync(item => item.YeuCauGiuChoId == id &&
                    item.TrangThai == TrangThaiYeuCauGiuCho.MoiTao);
            if (hold is null) return false;
            await context.PhongTros.FromSqlInterpolated(
                $"SELECT * FROM phong_tro WHERE \"PhongTroId\" = {hold.PhongTroId} FOR UPDATE").SingleAsync();
            if (actor.Role == Role.NhanVien && !await context.NhanVienChiNhanhs.AnyAsync(assignment =>
                    assignment.NguoiDungId == actorId && assignment.ChiNhanhId == hold.PhongTro.ChiNhanhId &&
                    assignment.IsActive && assignment.NgayThuHoi == null)) return false;

            var publicRoom = await PhongCongKhaiStore.FilterPublicRooms(
                    context.PhongTros.AsNoTracking(), context.YeuCauGiuChos.AsNoTracking())
                .AnyAsync(room => room.PhongTroId == hold.PhongTroId);
            if (!publicRoom || deadlineUtc <= DateTime.UtcNow || contractDeadlineUtc <= deadlineUtc) return false;
            var amount = GiuChoRules.TinhTienGiuCho(hold.PhongTro.GiaThue);
            if (amount <= 0) return false;

            hold.LichSuTrangThais.Add(new LichSuTrangThaiYeuCauGiuCho
            {
                TrangThaiTruoc = hold.TrangThai,
                TrangThaiSau = TrangThaiYeuCauGiuCho.ChoThanhToan,
                LoaiTacNhan = LoaiTacNhan.NguoiDung,
                NguoiThucHienId = actorId,
                NgayThucHien = DateTime.UtcNow
            });
            hold.TrangThai = TrangThaiYeuCauGiuCho.ChoThanhToan;
            hold.SoTienGiuCho = amount;
            hold.GiaThueDaChot = hold.PhongTro.GiaThue;
            hold.HanThanhToan = deadlineUtc;
            hold.HanKyHopDong = contractDeadlineUtc;
            hold.NguoiDuyetId = actorId;
            hold.NgayDuyet = DateTime.UtcNow;
            hold.NgayCapNhat = DateTime.UtcNow;
            context.YeuCauThanhToanGiuChos.Add(new YeuCauThanhToanGiuCho
            {
                YeuCauGiuChoId = hold.YeuCauGiuChoId,
                MaYeuCau = $"GC-{hold.YeuCauGiuChoId}",
                NoiDungChuyenKhoan = $"GIU CHO {hold.YeuCauGiuChoId}",
                SoTien = amount,
                HanThanhToan = deadlineUtc,
                TrangThai = TrangThaiYeuCauThanhToan.ChoThanhToan,
                NguoiTaoId = actorId,
                NgayTao = DateTime.UtcNow
            });
            await context.SaveChangesAsync();
            await transaction.CommitAsync();
            return true;
        }
        catch (Exception ex) when (DatabaseConflict.IsExpected(ex)) { return false; }
    }

    public async Task<IReadOnlyList<ReservationDto>> GetMineAsync(int actorId) =>
        await context.YeuCauGiuChos.AsNoTracking()
            .Where(hold => hold.KhachVangLai.NguoiDungId == actorId &&
                hold.KhachVangLai.NguoiDung.IsActive && !hold.KhachVangLai.NguoiDung.IsDeleted)
            .OrderByDescending(hold => hold.NgayTao)
            .Select(hold => new ReservationDto(hold.YeuCauGiuChoId,
                hold.PhongTro.SoPhong, hold.PhongTro.ChiNhanh.TenChiNhanh,
                hold.KhachVangLai.HoTen, hold.TrangThai.ToString(), hold.PhongTro.GiaThue, hold.SoTienGiuCho,
                hold.HanThanhToan, hold.HanKyHopDong,
                hold.YeuCauThanhToanGiuChos.Select(payment => payment.MaYeuCau).FirstOrDefault(),
                hold.YeuCauThanhToanGiuChos.Select(payment => payment.NoiDungChuyenKhoan).FirstOrDefault()))
            .ToListAsync();

    public async Task<bool> TryCloseUnpaidAsync(int id, int actorId, string reason)
    {
        try
        {
            await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var actor = await context.NguoiDungs.AsNoTracking().FirstOrDefaultAsync(item =>
                item.NguoiDungId == actorId && item.IsActive && !item.IsDeleted);
            if (actor is null || actor.Role is not (Role.Admin or Role.NhanVien)) return false;
            var hold = await context.YeuCauGiuChos
                .Include(item => item.YeuCauThanhToanGiuChos).ThenInclude(item => item.MinhChungThanhToanGiuChos)
                .Include(item => item.YeuCauThanhToanGiuChos).ThenInclude(item => item.GiaoDichGiuChos)
                .FirstOrDefaultAsync(item => item.YeuCauGiuChoId == id &&
                    (actor.Role == Role.Admin || context.NhanVienChiNhanhs.Any(assignment =>
                        assignment.NguoiDungId == actorId && assignment.IsActive && assignment.NgayThuHoi == null &&
                        assignment.ChiNhanhId == item.PhongTro.ChiNhanhId)));
            if (hold is null || hold.TrangThai is not (TrangThaiYeuCauGiuCho.MoiTao or TrangThaiYeuCauGiuCho.ChoThanhToan) ||
                hold.YeuCauThanhToanGiuChos.Any(item => item.MinhChungThanhToanGiuChos.Any() || item.GiaoDichGiuChos.Any()))
                return false;
            var now = DateTime.UtcNow;
            var next = hold.TrangThai == TrangThaiYeuCauGiuCho.MoiTao
                ? TrangThaiYeuCauGiuCho.TuChoi : TrangThaiYeuCauGiuCho.DaHuy;
            hold.LichSuTrangThais.Add(new LichSuTrangThaiYeuCauGiuCho
            {
                TrangThaiTruoc = hold.TrangThai, TrangThaiSau = next, LoaiTacNhan = LoaiTacNhan.NguoiDung,
                NguoiThucHienId = actorId, NgayThucHien = now, LyDo = reason
            });
            hold.TrangThai = next;
            hold.NgayCapNhat = now;
            hold.LyDoTuChoiHoacHuy = reason;
            foreach (var payment in hold.YeuCauThanhToanGiuChos)
            {
                payment.LichSuTrangThais.Add(new LichSuTrangThaiYeuCauThanhToanGiuCho
                {
                    TrangThaiTruoc = payment.TrangThai, TrangThaiSau = TrangThaiYeuCauThanhToan.DaHuy,
                    LoaiTacNhan = LoaiTacNhan.NguoiDung, NguoiThucHienId = actorId, NgayThucHien = now, LyDo = reason
                });
                payment.TrangThai = TrangThaiYeuCauThanhToan.DaHuy;
                payment.NgayCapNhat = now;
            }
            await context.SaveChangesAsync();
            await transaction.CommitAsync();
            return true;
        }
        catch (Exception ex) when (DatabaseConflict.IsExpected(ex)) { return false; }
    }

    public async Task<IReadOnlyList<ReservationDto>> GetQueueAsync(int actorId)
    {
        var actor = await context.NguoiDungs.AsNoTracking()
            .Where(user => user.NguoiDungId == actorId && user.IsActive && !user.IsDeleted)
            .Select(user => new { user.Role }).FirstOrDefaultAsync();
        if (actor is null || actor.Role is not (Role.Admin or Role.NhanVien))
            return Array.Empty<ReservationDto>();
        return await context.YeuCauGiuChos.AsNoTracking()
            .Where(hold => actor.Role == Role.Admin ||
                context.NhanVienChiNhanhs.Any(assignment => assignment.NguoiDungId == actorId &&
                    assignment.ChiNhanhId == hold.PhongTro.ChiNhanhId && assignment.IsActive &&
                    assignment.NgayThuHoi == null))
            .OrderByDescending(hold => hold.NgayTao)

            .Select(hold => new ReservationDto(hold.YeuCauGiuChoId,
                hold.PhongTro.SoPhong, hold.PhongTro.ChiNhanh.TenChiNhanh,
                hold.KhachVangLai.HoTen, hold.TrangThai.ToString(), hold.PhongTro.GiaThue, hold.SoTienGiuCho,
                hold.HanThanhToan, hold.HanKyHopDong,
                hold.YeuCauThanhToanGiuChos.Select(payment => payment.MaYeuCau).FirstOrDefault(),
                hold.YeuCauThanhToanGiuChos.Select(payment => payment.NoiDungChuyenKhoan).FirstOrDefault()))
            .ToListAsync();
    }
}
