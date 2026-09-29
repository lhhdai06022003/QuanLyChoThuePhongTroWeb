using Microsoft.EntityFrameworkCore;
using QuanLyChoThuePhongTroWeb.Application.Features.PublicRooms;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Features;

public sealed class PublicRoomStore(ApplicationDbContext context) : IPublicRoomStore
{
    public async Task<IReadOnlyList<PublicRoomDto>> ListAvailableAsync(
        CancellationToken cancellationToken = default) =>
        await AvailableRooms()
            .OrderBy(room => room.ChiNhanhId)
            .ThenBy(room => room.SoPhong)
            .Select(room => new PublicRoomDto(
                room.PhongTroId,
                room.SoPhong,
                room.ChiNhanh.TenChiNhanh,
                room.ChiNhanh.DiaChi,
                room.GiaThue,
                room.DienTich,
                room.SoNguoiToiDa,
                room.TangLau,
                room.MoTa))
            .ToListAsync(cancellationToken);

    public Task<PublicRoomDto?> GetAvailableAsync(int id,
        CancellationToken cancellationToken = default) =>
        AvailableRooms()
            .Where(room => room.PhongTroId == id)
            .Select(room => new PublicRoomDto(
                room.PhongTroId,
                room.SoPhong,
                room.ChiNhanh.TenChiNhanh,
                room.ChiNhanh.DiaChi,
                room.GiaThue,
                room.DienTich,
                room.SoNguoiToiDa,
                room.TangLau,
                room.MoTa))
            .FirstOrDefaultAsync(cancellationToken);

    private IQueryable<Domain.Entities.PhongTro> AvailableRooms() =>
        context.PhongTros.AsNoTracking()
            .Where(room => !room.IsDeleted && !room.ChiNhanh.IsDeleted &&
                room.TrangThai == TrangThaiPhong.Trong);
}
