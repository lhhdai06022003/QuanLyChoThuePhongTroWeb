using System.ComponentModel.DataAnnotations;

namespace QuanLyChoThuePhongTroWeb.Areas.KhachVangLai.ViewModels;

public sealed class HoSoViewModel
{
    public string HoTen { get; set; } = string.Empty;
    public string SoDienThoai { get; set; } = string.Empty;
    [Required, EmailAddress, StringLength(150)]
    public string Email { get; set; } = string.Empty;
    [Required, RegularExpression("^[0-9]{12}$", ErrorMessage = "CCCD phải gồm 12 chữ số.")]
    public string CCCD { get; set; } = string.Empty;
    public string? ReturnUrl { get; set; }
}
