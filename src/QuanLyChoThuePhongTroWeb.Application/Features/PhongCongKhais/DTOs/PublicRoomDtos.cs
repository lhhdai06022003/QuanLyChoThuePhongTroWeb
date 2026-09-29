namespace QuanLyChoThuePhongTroWeb.Application.Features.PhongCongKhais.DTOs;

public sealed record PublicRoomQuery(
    int? ChiNhanhId = null,
    decimal? GiaTu = null,
    decimal? GiaDen = null,
    double? DienTichTu = null,
    int Page = 1,
    int PageSize = 12);

public sealed record PublicRoomImageDto(string Url, string? ChuThich, bool LaAnhDaiDien, int ThuTuHienThi);

public sealed record PublicRoomDto(
    string MaCongKhai,
    string TieuDe,
    string SoPhong,
    string TenChiNhanh,
    string DiaChi,
    string SoDienThoaiLienHe,
    decimal GiaThue,
    double DienTich,
    int SoNguoiToiDa,
    string MoTa,
    IReadOnlyList<PublicRoomImageDto> AnhPhongs);

public sealed record PublicRoomPageDto(
    IReadOnlyList<PublicRoomDto> Items,
    int Page,
    int PageSize,
    int TotalCount);

// Data returned by persistence before the Application visibility check.
public sealed class PublicRoomRecord
{
    public int PhongTroId { get; init; }
    public int ChiNhanhId { get; init; }
    public string? MaCongKhai { get; init; }
    public string? TieuDe { get; init; }
    public string SoPhong { get; init; } = string.Empty;
    public string TenChiNhanh { get; init; } = string.Empty;
    public string DiaChi { get; init; } = string.Empty;
    public string SoDienThoaiLienHe { get; init; } = string.Empty;
    public decimal GiaThue { get; init; }
    public double DienTich { get; init; }
    public int SoNguoiToiDa { get; init; }
    public string MoTa { get; init; } = string.Empty;
    public bool DuocDangTin { get; init; }
    public bool PhongDaXoa { get; init; }
    public bool ChiNhanhDaXoa { get; init; }
    public bool PhongTrong { get; init; }
    public bool CoGiuChoHoatDong { get; init; }
    public IReadOnlyList<PublicRoomImageDto> AnhPhongs { get; init; } = Array.Empty<PublicRoomImageDto>();
}
