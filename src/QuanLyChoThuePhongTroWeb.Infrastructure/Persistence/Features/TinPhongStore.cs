using Microsoft.EntityFrameworkCore;
using Npgsql;
using QuanLyChoThuePhongTroWeb.Application.Features.PhongCongKhais.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.PhongCongKhais.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Features.PhongCongKhais.Services;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Features;

public sealed class TinPhongStore(ApplicationDbContext context) : ITinPhongStore
{
    public async Task<bool> CanManageAsync(int roomId, int actorId)
    {
        var role = await GetStaffRoleAsync(actorId);
        if (role is null) return false;
        return await context.PhongTros.AsNoTracking().AnyAsync(room =>
            room.PhongTroId == roomId && !room.IsDeleted && !room.ChiNhanh.IsDeleted &&
            (role == Role.Admin || context.NhanVienChiNhanhs.Any(assignment =>
                assignment.NguoiDungId == actorId && assignment.ChiNhanhId == room.ChiNhanhId &&
                assignment.IsActive && assignment.NgayThuHoi == null)));
    }

    public async Task<IReadOnlyList<ManagedRoomImageDto>> GetImagesAsync(int roomId, int actorId)
    {
        if (!await CanManageAsync(roomId, actorId)) return Array.Empty<ManagedRoomImageDto>();
        return await context.AnhPhongTros.AsNoTracking()
            .Where(image => image.PhongTroId == roomId && image.IsActive)
            .OrderByDescending(image => image.LaAnhDaiDien)
            .ThenBy(image => image.ThuTuHienThi)
            .Select(image => new ManagedRoomImageDto(image.AnhPhongTroId, image.Url,
                image.ChuThich, image.LaAnhDaiDien, image.ThuTuHienThi))
            .ToListAsync();
    }

    public async Task<bool> AddImageAsync(int roomId, int actorId, StoredRoomPhoto photo, string? caption)
    {
        if (!await CanManageAsync(roomId, actorId)) return false;
        var images = await context.AnhPhongTros.AsNoTracking()
            .Where(image => image.PhongTroId == roomId && image.IsActive)
            .Select(image => new { image.ThuTuHienThi, image.LaAnhDaiDien }).ToListAsync();
        context.AnhPhongTros.Add(new AnhPhongTro
        {
            PhongTroId = roomId,
            Url = photo.Url,
            PublicId = photo.PublicId,
            ChuThich = caption,
            ThuTuHienThi = images.Count == 0 ? 0 : images.Max(image => image.ThuTuHienThi) + 1,
            LaAnhDaiDien = !images.Any(image => image.LaAnhDaiDien),
            IsActive = true,
            NguoiTaiLenId = actorId,
            NgayTaiLen = DateTime.UtcNow
        });
        try
        {
            await context.SaveChangesAsync();
            return true;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg &&
            pg.SqlState == PostgresErrorCodes.UniqueViolation) { return false; }
    }

    public async Task<bool> SetCoverAsync(int roomId, int imageId, int actorId)
    {
        if (!await CanManageAsync(roomId, actorId)) return false;
        await using var transaction = await context.Database.BeginTransactionAsync();
        var images = await context.AnhPhongTros
            .Where(image => image.PhongTroId == roomId && image.IsActive).ToListAsync();
        var selected = images.FirstOrDefault(image => image.AnhPhongTroId == imageId);
        if (selected is null) return false;
        foreach (var image in images) image.LaAnhDaiDien = false;
        await context.SaveChangesAsync();
        selected.LaAnhDaiDien = true;
        await context.SaveChangesAsync();
        await transaction.CommitAsync();
        return true;
    }

    public async Task<bool> HideImageAsync(int roomId, int imageId, int actorId)
    {
        if (!await CanManageAsync(roomId, actorId)) return false;
        await using var transaction = await context.Database.BeginTransactionAsync();
        var images = await context.AnhPhongTros
            .Where(image => image.PhongTroId == roomId && image.IsActive)
            .OrderBy(image => image.ThuTuHienThi).ToListAsync();
        var selected = images.FirstOrDefault(image => image.AnhPhongTroId == imageId);
        if (selected is null) return false;
        var wasCover = selected.LaAnhDaiDien;
        selected.IsActive = false;
        selected.LaAnhDaiDien = false;
        await context.SaveChangesAsync();
        if (wasCover)
        {
            var next = images.FirstOrDefault(image => image.AnhPhongTroId != imageId);
            if (next is not null) next.LaAnhDaiDien = true;
            await context.SaveChangesAsync();
        }
        await transaction.CommitAsync();
        return true;
    }

    public async Task<IReadOnlyList<ManagedRoomDto>> GetManageableAsync(int actorId)
    {
        var role = await GetStaffRoleAsync(actorId);
        if (role is null) return Array.Empty<ManagedRoomDto>();
        return await context.PhongTros.AsNoTracking()
            .Where(room => !room.IsDeleted && !room.ChiNhanh.IsDeleted &&
                (role == Role.Admin || context.NhanVienChiNhanhs.Any(assignment =>
                    assignment.NguoiDungId == actorId && assignment.ChiNhanhId == room.ChiNhanhId &&
                    assignment.IsActive && assignment.NgayThuHoi == null)))
            .OrderBy(room => room.ChiNhanh.TenChiNhanh).ThenBy(room => room.SoPhong)
            .Select(room => new ManagedRoomDto(room.PhongTroId, room.SoPhong,
                room.ChiNhanh.TenChiNhanh, room.GiaThue, room.TrangThai.ToString(),
                room.DuocDangTin, room.TieuDeDangTin, room.MaCongKhai,
                room.AnhPhongTros.Count(image => image.IsActive)))
            .ToListAsync();
    }

    public async Task<bool> UpdateAsync(int roomId, int actorId, bool published, string? title)
    {
        var role = await GetStaffRoleAsync(actorId);
        if (role is null) return false;
        var room = await context.PhongTros.Include(item => item.ChiNhanh)
            .FirstOrDefaultAsync(item => item.PhongTroId == roomId && !item.IsDeleted);
        if (room is null || room.ChiNhanh.IsDeleted) return false;
        if (role == Role.NhanVien && !await context.NhanVienChiNhanhs.AnyAsync(assignment =>
                assignment.NguoiDungId == actorId && assignment.ChiNhanhId == room.ChiNhanhId &&
                assignment.IsActive && assignment.NgayThuHoi == null)) return false;
        if (published && (room.TrangThai != TrangThaiPhong.Trong ||
            await context.YeuCauGiuChos.AnyAsync(hold => hold.PhongTroId == roomId &&
                (hold.TrangThai == TrangThaiYeuCauGiuCho.ChoThanhToan ||
                 hold.TrangThai == TrangThaiYeuCauGiuCho.ChoXacNhanTien ||
                 hold.TrangThai == TrangThaiYeuCauGiuCho.DangGiuCho)))) return false;

        room.DuocDangTin = published;
        if (!string.IsNullOrWhiteSpace(title)) room.TieuDeDangTin = title;
        if (published)
        {
            room.MaCongKhai ??= $"p-{room.PhongTroId}-{Guid.NewGuid():N}";
            room.NguoiDangTinId = actorId;
        }
        room.NgayCapNhat = DateTime.UtcNow;
        try
        {
            await context.SaveChangesAsync();
            return true;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg &&
            pg.SqlState == PostgresErrorCodes.UniqueViolation) { return false; }
    }

    private async Task<Role?> GetStaffRoleAsync(int actorId) =>
        await context.NguoiDungs.AsNoTracking()
            .Where(user => user.NguoiDungId == actorId && user.IsActive && !user.IsDeleted &&
                (user.Role == Role.Admin || user.Role == Role.NhanVien))
            .Select(user => (Role?)user.Role).FirstOrDefaultAsync();
}
