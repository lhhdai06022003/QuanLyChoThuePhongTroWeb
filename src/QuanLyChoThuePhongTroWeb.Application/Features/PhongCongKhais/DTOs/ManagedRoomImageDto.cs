namespace QuanLyChoThuePhongTroWeb.Application.Features.PhongCongKhais.DTOs;

public sealed record ManagedRoomImageDto(int Id, string Url, string? ChuThich,
    bool LaAnhDaiDien, int ThuTuHienThi);
