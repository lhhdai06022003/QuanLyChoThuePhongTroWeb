using System.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using QuanLyChoThuePhongTroWeb.Application.Features.LichXemPhongs.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.LichXemPhongs.Persistence;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Features;

public sealed class KhungGioXemPhongStore(ApplicationDbContext context) : IKhungGioXemPhongStore
{
    public async Task<IReadOnlyList<ManagedViewingSlotDto>> GetManagedAsync(int actorId)
    {
        var role = await StaffRoleAsync(actorId);
        if (role is null) return [];
        return await context.KhungGioXemPhongs.AsNoTracking()
            .Where(slot => AuthorizedRooms(actorId, role.Value).Any(room => room.PhongTroId == slot.PhongTroId))
            .OrderByDescending(slot => slot.ThoiGianBatDau).Take(100)
            .Select(slot => new ManagedViewingSlotDto(slot.KhungGioXemPhongId,
                slot.PhongTro.SoPhong, slot.PhongTro.ChiNhanh.TenChiNhanh,
                slot.ThoiGianBatDau, slot.ThoiGianKetThuc, slot.SoLuongToiDa,
                slot.YeuCauXemPhongs.Count(request => request.TrangThai == TrangThaiYeuCauXemPhong.DaXacNhanLich ||
                    request.TrangThai == TrangThaiYeuCauXemPhong.ChoNhanVienXuLy), slot.IsActive)).ToListAsync();
    }

    public async Task<bool> CreateAsync(CreateViewingSlotRequest request, int actorId)
    {
        try
        {
            await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var role = await StaffRoleAsync(actorId);
            if (role is null || request.BatDau <= DateTimeOffset.UtcNow ||
                !await AuthorizedRooms(actorId, role.Value).AnyAsync(room => room.PhongTroId == request.PhongTroId) ||
                !await PhongCongKhaiStore.FilterPublicRooms(context.PhongTros, context.YeuCauGiuChos)
                    .AnyAsync(room => room.PhongTroId == request.PhongTroId)) return false;
            var start = request.BatDau.UtcDateTime;
            var end = request.KetThuc.UtcDateTime;
            if (await context.KhungGioXemPhongs.AnyAsync(slot => slot.PhongTroId == request.PhongTroId &&
                slot.IsActive && slot.ThoiGianBatDau < end && slot.ThoiGianKetThuc > start)) return false;
            context.KhungGioXemPhongs.Add(new KhungGioXemPhong
            {
                PhongTroId = request.PhongTroId, ThoiGianBatDau = start, ThoiGianKetThuc = end,
                SoLuongToiDa = request.SucChua, NguoiTaoId = actorId,
                NguoiPhuTrachId = actorId, NgayTao = DateTime.UtcNow
            });
            await context.SaveChangesAsync();
            await transaction.CommitAsync();
            return true;
        }
        catch (Exception ex) when (DatabaseConflict.IsExpected(ex)) { return false; }
    }

    public async Task<bool> CloseAsync(int id, int actorId)
    {
        await using var transaction = await context.Database.BeginTransactionAsync();
        var role = await StaffRoleAsync(actorId);
        if (role is null) return false;
        var slot = await context.KhungGioXemPhongs.FirstOrDefaultAsync(item => item.KhungGioXemPhongId == id &&
            AuthorizedRooms(actorId, role.Value).Any(room => room.PhongTroId == item.PhongTroId));
        if (slot is null) return false;
        slot.IsActive = false;
        slot.NgayCapNhat = DateTime.UtcNow;
        await context.SaveChangesAsync();
        await transaction.CommitAsync();
        return true;
    }

    private async Task<Role?> StaffRoleAsync(int actorId) => await context.NguoiDungs.AsNoTracking()
        .Where(user => user.NguoiDungId == actorId && user.IsActive && !user.IsDeleted &&
            (user.Role == Role.Admin || user.Role == Role.NhanVien))
        .Select(user => (Role?)user.Role).FirstOrDefaultAsync();

    private IQueryable<PhongTro> AuthorizedRooms(int actorId, Role role) => context.PhongTros
        .Where(room => !room.IsDeleted && (role == Role.Admin || context.NhanVienChiNhanhs.Any(assignment =>
            assignment.NguoiDungId == actorId && assignment.IsActive &&
            assignment.NgayThuHoi == null && assignment.ChiNhanhId == room.ChiNhanhId)));
}
