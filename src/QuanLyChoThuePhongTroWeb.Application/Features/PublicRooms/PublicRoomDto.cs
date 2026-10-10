namespace QuanLyChoThuePhongTroWeb.Application.Features.PublicRooms;

public sealed record PublicRoomDto(
    int PhongTroId,
    string SoPhong,
    string TenChiNhanh,
    string DiaChi,
    decimal GiaThue,
    double DienTich,
    int SoNguoiToiDa,
    int TangLau,
    string MoTa,
    string SoDienThoaiChiNhanh = "",
    IReadOnlyList<string>? ImageUrls = null);
