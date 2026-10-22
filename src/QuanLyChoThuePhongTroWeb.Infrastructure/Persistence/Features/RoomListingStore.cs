using Microsoft.EntityFrameworkCore;
using QuanLyChoThuePhongTroWeb.Application.Features.PublicRooms;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Features;

public sealed class RoomListingStore(ApplicationDbContext context) : IRoomListingStore
{
    public async Task<IReadOnlyList<RoomListingDto>> ListAsync(IReadOnlyCollection<int>? branchIds, CancellationToken ct)
    {
        var query = context.PhongTros.AsNoTracking().Where(p => !p.IsDeleted && !p.ChiNhanh.IsDeleted);
        if (branchIds is not null) query = query.Where(p => branchIds.Contains(p.ChiNhanhId));
        return await query.OrderBy(p => p.ChiNhanhId).ThenBy(p => p.SoPhong)
            .Select(p => new RoomListingDto(p.PhongTroId, p.ChiNhanhId, p.SoPhong, p.ChiNhanh.TenChiNhanh,
                p.GiaThue, p.DienTich, (int)p.TrangThai, !p.ChiNhanh.IsDeleted, p.DuocDangTin,
                p.TieuDeDangTin ?? "", p.MoTa ?? "", p.AnhPhongTros.Count(a => a.IsActive),
                p.AnhPhongTros.Where(a => a.IsActive).OrderByDescending(a => a.LaAnhDaiDien)
                    .ThenBy(a => a.ThuTuHienThi).Select(a => a.Url).FirstOrDefault())).ToListAsync(ct);
    }

    public Task<RoomListingDto?> GetAsync(int roomId, CancellationToken ct) =>
        context.PhongTros.AsNoTracking().Where(p => p.PhongTroId == roomId && !p.IsDeleted && !p.ChiNhanh.IsDeleted)
            .Select(p => new RoomListingDto(p.PhongTroId, p.ChiNhanhId, p.SoPhong, p.ChiNhanh.TenChiNhanh,
                p.GiaThue, p.DienTich, (int)p.TrangThai, !p.ChiNhanh.IsDeleted, p.DuocDangTin,
                p.TieuDeDangTin ?? "", p.MoTa ?? "", p.AnhPhongTros.Count(a => a.IsActive), null))
            .SingleOrDefaultAsync(ct);

    public async Task<bool> SaveAsync(int actorId, int branchId, SaveRoomListing request,
        IReadOnlyList<ListingPhoto> photos, CancellationToken ct)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(ct);
        // Serialize uploads for the same room before counting images and choosing its cover.
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM phong_tro WHERE \"PhongTroId\" = {request.RoomId} FOR UPDATE", ct);
        var room = await context.PhongTros.Include(p => p.ChiNhanh).Include(p => p.AnhPhongTros)
            .SingleOrDefaultAsync(p => p.PhongTroId == request.RoomId && !p.IsDeleted, ct);
        if (room is null || room.ChiNhanhId != branchId || room.ChiNhanh.IsDeleted) return false;
        var active = room.AnhPhongTros.Where(a => a.IsActive).ToArray();
        if (active.Length + photos.Count > 10 ||
            (request.Publish && !room.DuocDangTin && room.TrangThai != TrangThaiPhong.Trong)) return false;
        room.TieuDeDangTin = request.Title;
        room.MoTa = request.Description;
        room.DuocDangTin = request.Publish;
        room.NguoiDangTinId = actorId;
        room.NgayCapNhat = DateTime.UtcNow;
        var next = active.Select(a => a.ThuTuHienThi).DefaultIfEmpty(-1).Max() + 1;
        var needsCover = !active.Any(a => a.LaAnhDaiDien);
        foreach (var image in photos)
        {
            context.AnhPhongTros.Add(new AnhPhongTro { PhongTroId = room.PhongTroId,
                Url = image.Url, PublicId = image.PublicId, NguoiTaiLenId = actorId,
                ThuTuHienThi = next++, LaAnhDaiDien = needsCover, IsActive = true });
            needsCover = false;
        }
        await context.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return true;
    }
}
