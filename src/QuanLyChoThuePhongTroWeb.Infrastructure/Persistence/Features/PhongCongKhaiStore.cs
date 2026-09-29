using Microsoft.EntityFrameworkCore;
using QuanLyChoThuePhongTroWeb.Application.Features.PhongCongKhais.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.PhongCongKhais.Persistence;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Features;

public sealed class PhongCongKhaiStore(ApplicationDbContext context) : IPhongCongKhaiStore
{
    // Keep the same predicate for the list, detail page, and later guest actions.
    public static IQueryable<PhongTro> FilterPublicRooms(
        IQueryable<PhongTro> rooms, IQueryable<YeuCauGiuCho> holds) =>
        rooms.Where(room => room.DuocDangTin && !room.IsDeleted &&
            !room.ChiNhanh.IsDeleted && room.TrangThai == TrangThaiPhong.Trong &&
            room.MaCongKhai != null &&
            !holds.Any(hold => hold.PhongTroId == room.PhongTroId &&
                (hold.TrangThai == TrangThaiYeuCauGiuCho.ChoThanhToan ||
                 hold.TrangThai == TrangThaiYeuCauGiuCho.ChoXacNhanTien ||
                 hold.TrangThai == TrangThaiYeuCauGiuCho.DangGiuCho)));

    public async Task<IReadOnlyList<PublicRoomRecord>> GetCandidatesAsync()
    {
        var rooms = await PublicQuery()
            .OrderByDescending(room => room.PhongTroId)
            .ToListAsync();
        return rooms.Select(ToRecord).ToList();
    }

    public async Task<PublicRoomRecord?> GetByCodeAsync(string code)
    {
        var room = await PublicQuery()
            .FirstOrDefaultAsync(room => room.MaCongKhai == code);
        return room is null ? null : ToRecord(room);
    }

    private IQueryable<PhongTro> PublicQuery() =>
        FilterPublicRooms(context.PhongTros.AsNoTracking(), context.YeuCauGiuChos.AsNoTracking())
            .Include(room => room.ChiNhanh)
            .Include(room => room.AnhPhongTros)
            .AsSplitQuery();

    private static PublicRoomRecord ToRecord(PhongTro room) => new()
    {
        PhongTroId = room.PhongTroId,
        ChiNhanhId = room.ChiNhanhId,
        MaCongKhai = room.MaCongKhai,
        TieuDe = room.TieuDeDangTin,
        SoPhong = room.SoPhong,
        TenChiNhanh = room.ChiNhanh.TenChiNhanh,
        DiaChi = room.ChiNhanh.DiaChi,
        SoDienThoaiLienHe = room.ChiNhanh.SoDienThoai,
        GiaThue = room.GiaThue,
        DienTich = room.DienTich,
        SoNguoiToiDa = room.SoNguoiToiDa,
        MoTa = room.MoTa ?? string.Empty,
        DuocDangTin = room.DuocDangTin,
        PhongDaXoa = room.IsDeleted,
        ChiNhanhDaXoa = room.ChiNhanh.IsDeleted,
        PhongTrong = room.TrangThai == TrangThaiPhong.Trong,
        CoGiuChoHoatDong = false,
        AnhPhongs = room.AnhPhongTros
            .Where(image => image.IsActive)
            .Select(image => new PublicRoomImageDto(image.Url, image.ChuThich,
                image.LaAnhDaiDien, image.ThuTuHienThi))
            .ToList()
    };
}
