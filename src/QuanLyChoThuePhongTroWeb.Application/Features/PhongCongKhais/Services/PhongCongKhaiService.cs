using QuanLyChoThuePhongTroWeb.Application.Features.PhongCongKhais.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.PhongCongKhais.Persistence;

namespace QuanLyChoThuePhongTroWeb.Application.Features.PhongCongKhais.Services;

public sealed class PhongCongKhaiService(IPhongCongKhaiStore store) : IPhongCongKhaiService
{
    public async Task<PublicRoomPageDto> SearchAsync(PublicRoomQuery query)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 50);
        var records = await store.GetCandidatesAsync();
        var visible = records.Where(IsVisible);

        if (query.ChiNhanhId is > 0)
            visible = visible.Where(room => room.ChiNhanhId == query.ChiNhanhId);
        if (query.GiaTu is >= 0)
            visible = visible.Where(room => room.GiaThue >= query.GiaTu);
        if (query.GiaDen is >= 0)
            visible = visible.Where(room => room.GiaThue <= query.GiaDen);
        if (query.DienTichTu is >= 0)
            visible = visible.Where(room => room.DienTich >= query.DienTichTu);

        var sorted = visible.OrderByDescending(room => room.PhongTroId).ToList();
        var items = sorted.Skip((page - 1) * pageSize).Take(pageSize).Select(ToDto).ToList();
        return new PublicRoomPageDto(items, page, pageSize, sorted.Count);
    }

    public async Task<PublicRoomDto?> GetAsync(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;
        var room = await store.GetByCodeAsync(code);
        return room is not null && IsVisible(room) ? ToDto(room) : null;
    }

    private static bool IsVisible(PublicRoomRecord room) =>
        room.DuocDangTin && !room.PhongDaXoa && !room.ChiNhanhDaXoa &&
        room.PhongTrong && !room.CoGiuChoHoatDong &&
        !string.IsNullOrWhiteSpace(room.MaCongKhai);

    private static PublicRoomDto ToDto(PublicRoomRecord room) => new(
        room.MaCongKhai!,
        string.IsNullOrWhiteSpace(room.TieuDe) ? $"Phòng {room.SoPhong}" : room.TieuDe,
        room.SoPhong,
        room.TenChiNhanh,
        room.DiaChi,
        room.SoDienThoaiLienHe,
        room.GiaThue,
        room.DienTich,
        room.SoNguoiToiDa,
        room.MoTa,
        room.AnhPhongs.OrderByDescending(image => image.LaAnhDaiDien)
            .ThenBy(image => image.ThuTuHienThi).ToList());
}
