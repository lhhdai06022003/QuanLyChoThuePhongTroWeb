using Microsoft.EntityFrameworkCore;
using QuanLyChoThuePhongTroWeb.Application.Features.PublicRooms;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Features;

public sealed class PublicRoomStore(ApplicationDbContext context, TimeProvider clock) : IPublicRoomStore
{
    public async Task<IReadOnlyList<PublicRoomDto>> ListAvailableAsync(
        CancellationToken cancellationToken = default)
    {
        var rooms = await AvailableRooms()
            .OrderBy(room => room.ChiNhanhId)
            .ThenBy(room => room.SoPhong)
            .ToListAsync(cancellationToken);
        return rooms.Select(ToDto).ToArray();
    }

    public async Task<PublicRoomDto?> GetAvailableAsync(int id,
        CancellationToken cancellationToken = default)
    {
        var room = await AvailableRooms()
            .FirstOrDefaultAsync(room => room.PhongTroId == id, cancellationToken);
        return room is null ? null : ToDto(room);
    }

    private IQueryable<PhongTro> AvailableRooms()
    {
        var nowUtc = clock.GetUtcNow().UtcDateTime;
        return context.PhongTros.AsNoTracking()
            .AsSplitQuery()
            .Include(room => room.ChiNhanh)
            .Include(room => room.AnhPhongTros)
            .Where(room => !room.IsDeleted && !room.ChiNhanh.IsDeleted &&
                room.DuocDangTin && room.TrangThai == TrangThaiPhong.Trong &&
                !context.YeuCauGiuChos.Any(hold => hold.PhongTroId == room.PhongTroId &&
                    (hold.TrangThai == TrangThaiYeuCauGiuCho.ChoThanhToan &&
                        hold.HanThanhToan > nowUtc ||
                     hold.TrangThai == TrangThaiYeuCauGiuCho.ChoXacNhanTien ||
                     hold.TrangThai == TrangThaiYeuCauGiuCho.DangGiuCho)));
    }

    private static PublicRoomDto ToDto(PhongTro room) => new(
        room.PhongTroId, room.SoPhong, room.ChiNhanh.TenChiNhanh,
        room.ChiNhanh.DiaChi, room.GiaThue, room.DienTich,
        room.SoNguoiToiDa, room.TangLau, room.MoTa, room.ChiNhanh.SoDienThoai,
        room.AnhPhongTros
            .Where(image => image.IsActive && IsSafeImageUrl(image.Url))
            .OrderByDescending(image => image.LaAnhDaiDien)
            .ThenBy(image => image.ThuTuHienThi)
            .Take(10)
            .Select(image => image.Url)
            .ToArray());

    private static bool IsSafeImageUrl(string? url) =>
        !string.IsNullOrWhiteSpace(url) &&
        ((url.StartsWith('/') && !url.StartsWith("//")) ||
         (Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
          (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)));
}
