namespace QuanLyChoThuePhongTroWeb.Application.Features.PublicRooms;

public sealed record PublicRoomDto(
    int PhongTroId,
    string SoPhong,
    string TenChiNhanh,
    string DiaChi,
    double GiaThue,
    double DienTich,
    int SoNguoiToiDa,
    int TangLau,
    string MoTa);
