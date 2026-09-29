using System.ComponentModel.DataAnnotations;
using QuanLyChoThuePhongTroWeb.Application.Features.LichXemPhongs.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.PhongCongKhais.DTOs;

namespace QuanLyChoThuePhongTroWeb.Areas.KhachVangLai.ViewModels;

public sealed class TaoLichXemViewModel
{
    [Required]
    public string MaCongKhai { get; set; } = string.Empty;

    public PublicRoomDto? Phong { get; set; }
    public IReadOnlyList<ViewingSlotDto> KhungGios { get; set; } = Array.Empty<ViewingSlotDto>();
    public int? KhungGioXemPhongId { get; set; }
    public DateTime? ThoiGianMongMuon { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập họ tên.")]
    [StringLength(100, MinimumLength = 2)]
    public string HoTen { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập số điện thoại.")]
    [RegularExpression("^[0-9]{9,20}$", ErrorMessage = "Số điện thoại chỉ gồm 9–20 chữ số.")]
    public string SoDienThoai { get; set; } = string.Empty;

    [EmailAddress]
    [StringLength(150)]
    public string? Email { get; set; }

    [StringLength(1000)]
    public string? GhiChu { get; set; }
}
