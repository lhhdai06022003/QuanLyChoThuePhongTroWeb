using System.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using QuanLyChoThuePhongTroWeb.Application.Features.Viewings;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Features.Viewings;

public sealed class ViewingSlotAdminStore(ApplicationDbContext context, TimeProvider clock) : IViewingSlotAdminStore
{
    public Task<int?> GetPublishedRoomBranchIdAsync(int roomId,
        CancellationToken cancellationToken = default) =>
        PublishedRooms(clock.GetUtcNow().UtcDateTime).Where(room => room.PhongTroId == roomId)
            .Select(room => (int?)room.ChiNhanhId)
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<int?> TryCreateAsync(int actorId, int roomId, DateTime startUtc,
        DateTime endUtc, int capacity, DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var transaction = await context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, cancellationToken);
            if (!await PublishedRooms(nowUtc).AnyAsync(room => room.PhongTroId == roomId,
                    cancellationToken))
                return null;

            var overlaps = await context.KhungGioXemPhongs.AsNoTracking()
                .AnyAsync(slot => slot.PhongTroId == roomId && slot.IsActive &&
                    slot.ThoiGianBatDau < endUtc && slot.ThoiGianKetThuc > startUtc,
                    cancellationToken);
            if (overlaps)
                return null;

            var slot = new KhungGioXemPhong
            {
                PhongTroId = roomId,
                ThoiGianBatDau = startUtc,
                ThoiGianKetThuc = endUtc,
                SoLuongToiDa = capacity,
                NguoiTaoId = actorId,
                IsActive = true,
                NgayTao = nowUtc
            };
            context.KhungGioXemPhongs.Add(slot);
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return slot.KhungGioXemPhongId;
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.SerializationFailure)
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<ViewingSlotAdminDto>> ListAsync(
        IReadOnlyCollection<int> roomIds, DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        if (roomIds.Count == 0)
            return [];

        var slots = await context.KhungGioXemPhongs.AsNoTracking()
            .Where(slot => roomIds.Contains(slot.PhongTroId) && slot.IsActive &&
                slot.ThoiGianBatDau > nowUtc)
            .Select(slot => new
            {
                slot.KhungGioXemPhongId,
                slot.PhongTroId,
                slot.ThoiGianBatDau,
                slot.ThoiGianKetThuc,
                slot.SoLuongToiDa,
                Booked = context.YeuCauXemPhongs.Count(request =>
                    request.KhungGioXemPhongId == slot.KhungGioXemPhongId &&
                    request.TrangThai == TrangThaiYeuCauXemPhong.DaXacNhanLich)
            })
            .OrderBy(slot => slot.ThoiGianBatDau)
            .ToListAsync(cancellationToken);

        return slots.Select(slot => new ViewingSlotAdminDto(
            slot.KhungGioXemPhongId, slot.PhongTroId, slot.ThoiGianBatDau,
            slot.ThoiGianKetThuc, slot.SoLuongToiDa, slot.Booked)).ToArray();
    }

    private IQueryable<PhongTro> PublishedRooms(DateTime nowUtc) => context.PhongTros.AsNoTracking()
        .Where(room => !room.IsDeleted && !room.ChiNhanh.IsDeleted &&
            room.DuocDangTin && room.TrangThai == TrangThaiPhong.Trong &&
            !context.YeuCauGiuChos.Any(hold => hold.PhongTroId == room.PhongTroId &&
                (hold.TrangThai == TrangThaiYeuCauGiuCho.ChoThanhToan &&
                    hold.HanThanhToan > nowUtc ||
                 hold.TrangThai == TrangThaiYeuCauGiuCho.ChoXacNhanTien ||
                 hold.TrangThai == TrangThaiYeuCauGiuCho.DangGiuCho)));
}
