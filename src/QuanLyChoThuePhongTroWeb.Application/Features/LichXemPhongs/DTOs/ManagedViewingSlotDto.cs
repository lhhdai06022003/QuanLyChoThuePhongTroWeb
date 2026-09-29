namespace QuanLyChoThuePhongTroWeb.Application.Features.LichXemPhongs.DTOs;

public sealed record ManagedViewingSlotDto(int Id, string TenPhong, string TenChiNhanh,
    DateTime BatDauUtc, DateTime KetThucUtc, int SucChua, int DaDat, bool IsActive);
public sealed record CreateViewingSlotRequest(int PhongTroId, DateTimeOffset BatDau,
    DateTimeOffset KetThuc, int SucChua);
