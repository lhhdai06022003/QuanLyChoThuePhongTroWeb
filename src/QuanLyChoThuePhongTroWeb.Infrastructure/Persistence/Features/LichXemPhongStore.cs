using System.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using QuanLyChoThuePhongTroWeb.Application.Features.LichXemPhongs.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.LichXemPhongs.Persistence;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Features;

public sealed class LichXemPhongStore(ApplicationDbContext context) : ILichXemPhongStore
{
    public async Task<IReadOnlyList<GuestViewingDto>> GetMineAsync(int actorId) =>
        await context.YeuCauXemPhongs.AsNoTracking()
            .Where(request => request.KhachVangLai != null &&
                request.KhachVangLai.NguoiDungId == actorId &&
                request.KhachVangLai.NguoiDung.IsActive &&
                !request.KhachVangLai.NguoiDung.IsDeleted)
            .OrderByDescending(request => request.NgayTao)
            .Select(request => new GuestViewingDto(request.YeuCauXemPhongId,
                request.PhongTro.SoPhong, request.PhongTro.ChiNhanh.TenChiNhanh,
                request.PhongTro.MaCongKhai, request.ThoiGianMongMuon,
                request.TrangThai.ToString()))
            .ToListAsync();

    public async Task<IReadOnlyList<StaffViewingDto>> GetStaffQueueAsync(int actorId)
    {
        var actor = await context.NguoiDungs.AsNoTracking()
            .Where(user => user.NguoiDungId == actorId && user.IsActive && !user.IsDeleted)
            .Select(user => new { user.Role }).FirstOrDefaultAsync();
        if (actor is null || actor.Role is not (Role.Admin or Role.NhanVien))
            return Array.Empty<StaffViewingDto>();

        return await context.YeuCauXemPhongs.AsNoTracking()
            .Where(request => actor.Role == Role.Admin ||
                context.NhanVienChiNhanhs.Any(assignment => assignment.NguoiDungId == actorId &&
                    assignment.ChiNhanhId == request.PhongTro.ChiNhanhId && assignment.IsActive &&
                    assignment.NgayThuHoi == null))
            .OrderByDescending(request => request.NgayTao)

            .Select(request => new StaffViewingDto(request.YeuCauXemPhongId,
                request.PhongTro.SoPhong, request.PhongTro.ChiNhanh.TenChiNhanh,
                request.HoTen, request.SoDienThoai, request.Email,
                request.ThoiGianMongMuon, request.GhiChu, request.TrangThai.ToString()))
            .ToListAsync();
    }

    public async Task<bool> DecideAsync(int requestId, int actorId, bool confirm)
    {
        try
        {
            await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var actor = await context.NguoiDungs.AsNoTracking()
                .Where(user => user.NguoiDungId == actorId && user.IsActive && !user.IsDeleted)
                .Select(user => new { user.Role }).FirstOrDefaultAsync();
            if (actor is null || actor.Role is not (Role.Admin or Role.NhanVien)) return false;

            var request = await context.YeuCauXemPhongs.Include(item => item.PhongTro)
                .FirstOrDefaultAsync(item => item.YeuCauXemPhongId == requestId &&
                    item.TrangThai == TrangThaiYeuCauXemPhong.ChoNhanVienXuLy);
            if (request is null) return false;
            if (actor.Role == Role.NhanVien && !await context.NhanVienChiNhanhs.AnyAsync(assignment =>
                    assignment.NguoiDungId == actorId && assignment.ChiNhanhId == request.PhongTro.ChiNhanhId &&
                    assignment.IsActive && assignment.NgayThuHoi == null)) return false;

            if (confirm)
            {
                var visible = await PhongCongKhaiStore.FilterPublicRooms(
                    context.PhongTros.AsNoTracking(), context.YeuCauGiuChos.AsNoTracking())
                    .AnyAsync(room => room.PhongTroId == request.PhongTroId);
                if (!visible || request.ThoiGianMongMuon <= DateTime.UtcNow) return false;
            }

            var next = confirm ? TrangThaiYeuCauXemPhong.DaXacNhanLich : TrangThaiYeuCauXemPhong.TuChoi;
            request.LichSuTrangThais.Add(new LichSuTrangThaiYeuCauXemPhong
            {
                TrangThaiTruoc = request.TrangThai,
                TrangThaiSau = next,
                LoaiTacNhan = LoaiTacNhan.NguoiDung,
                NguoiThucHienId = actorId,
                NgayThucHien = DateTime.UtcNow
            });
            request.TrangThai = next;
            request.ThoiGianXacNhan = confirm ? request.ThoiGianMongMuon : null;
            request.NgayCapNhat = DateTime.UtcNow;
            await context.SaveChangesAsync();
            await transaction.CommitAsync();
            return true;
        }
        catch (Exception ex) when (DatabaseConflict.IsExpected(ex))
        { context.ChangeTracker.Clear(); return false; }
    }

    public async Task<IReadOnlyList<ViewingSlotDto>> GetOpenSlotsAsync(string code, DateTime nowUtc)
    {
        var publicRooms = PhongCongKhaiStore.FilterPublicRooms(
            context.PhongTros.AsNoTracking(), context.YeuCauGiuChos.AsNoTracking());
        return await context.KhungGioXemPhongs.AsNoTracking()
            .Where(slot => slot.IsActive && slot.ThoiGianBatDau > nowUtc &&
                publicRooms.Any(room => room.PhongTroId == slot.PhongTroId && room.MaCongKhai == code))
            .Where(slot => slot.SoLuongToiDa > context.YeuCauXemPhongs.Count(request =>
                request.KhungGioXemPhongId == slot.KhungGioXemPhongId &&
                (request.TrangThai == TrangThaiYeuCauXemPhong.ChoNhanVienXuLy ||
                 request.TrangThai == TrangThaiYeuCauXemPhong.DaXacNhanLich)))
            .OrderBy(slot => slot.ThoiGianBatDau)
            .Select(slot => new ViewingSlotDto(slot.KhungGioXemPhongId,
                slot.ThoiGianBatDau, slot.ThoiGianKetThuc,
                slot.SoLuongToiDa - context.YeuCauXemPhongs.Count(request =>
                    request.KhungGioXemPhongId == slot.KhungGioXemPhongId &&
                    (request.TrangThai == TrangThaiYeuCauXemPhong.ChoNhanVienXuLy ||
                     request.TrangThai == TrangThaiYeuCauXemPhong.DaXacNhanLich))))
            .ToListAsync();
    }

    public async Task<IReadOnlyList<ViewingSlotDto>> GetRescheduleSlotsAsync(
        int requestId, int actorId, DateTime nowUtc)
    {
        var actor = await context.NguoiDungs.AsNoTracking()
            .Where(user => user.NguoiDungId == actorId && user.IsActive && !user.IsDeleted &&
                (user.Role == Role.Admin || user.Role == Role.NhanVien))
            .Select(user => new { user.Role }).FirstOrDefaultAsync();
        if (actor is null) return Array.Empty<ViewingSlotDto>();
        var viewing = await context.YeuCauXemPhongs.AsNoTracking()
            .Where(item => item.YeuCauXemPhongId == requestId &&
                item.TrangThai == TrangThaiYeuCauXemPhong.DaXacNhanLich &&
                (actor.Role == Role.Admin || context.NhanVienChiNhanhs.Any(assignment =>
                    assignment.NguoiDungId == actorId && assignment.IsActive &&
                    assignment.NgayThuHoi == null && assignment.ChiNhanhId == item.PhongTro.ChiNhanhId)))
            .Select(item => new { item.PhongTroId, item.KhungGioXemPhongId }).FirstOrDefaultAsync();
        if (viewing is null || !await PhongCongKhaiStore.FilterPublicRooms(
                context.PhongTros.AsNoTracking(), context.YeuCauGiuChos.AsNoTracking())
            .AnyAsync(room => room.PhongTroId == viewing.PhongTroId))
            return Array.Empty<ViewingSlotDto>();

        return await context.KhungGioXemPhongs.AsNoTracking()
            .Where(slot => slot.PhongTroId == viewing.PhongTroId && slot.IsActive &&
                slot.KhungGioXemPhongId != viewing.KhungGioXemPhongId &&
                slot.ThoiGianBatDau > nowUtc)
            .Where(slot => slot.SoLuongToiDa > context.YeuCauXemPhongs.Count(request =>
                request.KhungGioXemPhongId == slot.KhungGioXemPhongId &&
                (request.TrangThai == TrangThaiYeuCauXemPhong.ChoNhanVienXuLy ||
                 request.TrangThai == TrangThaiYeuCauXemPhong.DaXacNhanLich)))
            .OrderBy(slot => slot.ThoiGianBatDau)
            .Select(slot => new ViewingSlotDto(slot.KhungGioXemPhongId,
                slot.ThoiGianBatDau, slot.ThoiGianKetThuc,
                slot.SoLuongToiDa - context.YeuCauXemPhongs.Count(request =>
                    request.KhungGioXemPhongId == slot.KhungGioXemPhongId &&
                    (request.TrangThai == TrangThaiYeuCauXemPhong.ChoNhanVienXuLy ||
                     request.TrangThai == TrangThaiYeuCauXemPhong.DaXacNhanLich))))
            .ToListAsync();
    }

    public async Task<bool> ManageConfirmedAsync(int requestId, int actorId, ManageViewingRequest command)
    {
        try
        {
            await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var actor = await context.NguoiDungs.AsNoTracking().FirstOrDefaultAsync(user =>
                user.NguoiDungId == actorId && user.IsActive && !user.IsDeleted &&
                (user.Role == Role.Admin || user.Role == Role.NhanVien));
            if (actor is null || !Enum.IsDefined(command.Action) ||
                string.IsNullOrWhiteSpace(command.Reason) || command.Reason.Length > 500) return false;
            var viewing = await context.YeuCauXemPhongs.Include(item => item.PhongTro)
                .FirstOrDefaultAsync(item => item.YeuCauXemPhongId == requestId &&
                    item.TrangThai == TrangThaiYeuCauXemPhong.DaXacNhanLich);
            if (viewing is null || (actor.Role != Role.Admin &&
                !await context.NhanVienChiNhanhs.AnyAsync(assignment => assignment.NguoiDungId == actorId &&
                    assignment.IsActive && assignment.NgayThuHoi == null &&
                    assignment.ChiNhanhId == viewing.PhongTro.ChiNhanhId))) return false;
            var next = command.Action switch
            {
                ViewingStaffAction.Cancel => TrangThaiYeuCauXemPhong.DaHuy,
                ViewingStaffAction.Complete => TrangThaiYeuCauXemPhong.DaXemPhong,
                _ => TrangThaiYeuCauXemPhong.DaXacNhanLich
            };
            if (command.Action == ViewingStaffAction.Complete && viewing.ThoiGianMongMuon > DateTime.UtcNow)
                return false;
            var reason = command.Reason.Trim();
            if (command.Action == ViewingStaffAction.Reschedule)
            {
                if (!await PhongCongKhaiStore.FilterPublicRooms(context.PhongTros.AsNoTracking(),
                        context.YeuCauGiuChos.AsNoTracking()).AnyAsync(room => room.PhongTroId == viewing.PhongTroId))
                    return false;
                var newTimeUtc = command.NewTimeUtc;
                int? newSlotId = null;
                if (command.NewSlotId is int slotId)
                {
                    var slot = await context.KhungGioXemPhongs.AsNoTracking()
                        .FirstOrDefaultAsync(item => item.KhungGioXemPhongId == slotId &&
                            item.PhongTroId == viewing.PhongTroId && item.IsActive &&
                            item.ThoiGianBatDau > DateTime.UtcNow);
                    if (slot is null || viewing.KhungGioXemPhongId == slotId ||
                        await context.YeuCauXemPhongs.CountAsync(item =>
                            item.KhungGioXemPhongId == slotId &&
                            (item.TrangThai == TrangThaiYeuCauXemPhong.ChoNhanVienXuLy ||
                             item.TrangThai == TrangThaiYeuCauXemPhong.DaXacNhanLich)) >= slot.SoLuongToiDa)
                        return false;
                    newTimeUtc = slot.ThoiGianBatDau;
                    newSlotId = slotId;
                }
                if (newTimeUtc is null || newTimeUtc <= DateTime.UtcNow) return false;
                reason = $"Đổi lịch {viewing.ThoiGianMongMuon:O} → {newTimeUtc:O}. {reason}";
                if (reason.Length > 500) return false;
                viewing.KhungGioXemPhongId = newSlotId;
                viewing.ThoiGianMongMuon = newTimeUtc.Value;
                viewing.ThoiGianXacNhan = newTimeUtc;
            }
            viewing.LichSuTrangThais.Add(new LichSuTrangThaiYeuCauXemPhong
            {
                TrangThaiTruoc = viewing.TrangThai, TrangThaiSau = next,
                LoaiTacNhan = LoaiTacNhan.NguoiDung, NguoiThucHienId = actorId,
                NgayThucHien = DateTime.UtcNow, LyDo = reason
            });
            viewing.TrangThai = next;
            viewing.NgayCapNhat = DateTime.UtcNow;
            await context.SaveChangesAsync();
            await transaction.CommitAsync();
            return true;
        }
        catch (Exception ex) when (DatabaseConflict.IsExpected(ex))
        { context.ChangeTracker.Clear(); return false; }
    }

    public async Task<bool> CreateAsync(CreateViewingCommand command)
    {
        try
        {
            await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var publicRooms = PhongCongKhaiStore.FilterPublicRooms(
                context.PhongTros.AsNoTracking(), context.YeuCauGiuChos.AsNoTracking());
            var roomId = await publicRooms.Where(room => room.MaCongKhai == command.MaCongKhai)
                .Select(room => (int?)room.PhongTroId).FirstOrDefaultAsync();
            if (roomId is null) return false;

            int? guestId = null;
            if (command.NguoiDungId is int userId)
            {
                guestId = (await BookingCustomerProfile.GetOrCreateAsync(context, userId))?.KhachVangLaiId;
                if (guestId is null) return false;
            }

            if (command.KhungGioXemPhongId is int slotId)
            {
                var slot = await context.KhungGioXemPhongs.AsNoTracking()
                    .FirstOrDefaultAsync(item => item.KhungGioXemPhongId == slotId &&
                        item.PhongTroId == roomId && item.IsActive &&
                        item.ThoiGianBatDau > DateTime.UtcNow);
                if (slot is null) return false;

                var occupied = await context.YeuCauXemPhongs.CountAsync(item =>
                    item.KhungGioXemPhongId == slotId &&
                    (item.TrangThai == TrangThaiYeuCauXemPhong.ChoNhanVienXuLy ||
                     item.TrangThai == TrangThaiYeuCauXemPhong.DaXacNhanLich));
                if (occupied >= slot.SoLuongToiDa) return false;
            }

            var request = new YeuCauXemPhong
            {
                PhongTroId = roomId.Value,
                KhachVangLaiId = guestId,
                KhungGioXemPhongId = command.KhungGioXemPhongId,
                ThoiGianMongMuon = command.ThoiGianMongMuonUtc,
                HoTen = command.HoTen,
                SoDienThoai = command.SoDienThoai,
                Email = command.Email,
                GhiChu = command.GhiChu,
                NguonTao = NguonTaoYeuCauXemPhong.TrangCongKhai,
                TrangThai = command.KhungGioXemPhongId is null
                    ? TrangThaiYeuCauXemPhong.ChoNhanVienXuLy
                    : TrangThaiYeuCauXemPhong.DaXacNhanLich,
                ThoiGianXacNhan = command.KhungGioXemPhongId is null
                    ? null : command.ThoiGianMongMuonUtc,
                NgayTao = DateTime.UtcNow
            };
            request.LichSuTrangThais.Add(new LichSuTrangThaiYeuCauXemPhong
            {
                TrangThaiSau = request.TrangThai,
                LoaiTacNhan = LoaiTacNhan.HeThong,
                NgayThucHien = request.NgayTao
            });
            context.YeuCauXemPhongs.Add(request);
            await context.SaveChangesAsync();
            await transaction.CommitAsync();
            return true;
        }
        catch (Exception ex) when (DatabaseConflict.IsExpected(ex)) { return false; }
    }
}
