using System.ComponentModel.DataAnnotations;

namespace QuanLyChoThuePhongTroWeb.Areas.KhachVangLai.ViewModels;

public sealed class DangKyViewModel
{
    public string? ReturnUrl { get; set; }

    [Required, RegularExpression("^[a-zA-Z0-9._]{4,50}$", ErrorMessage = "Dùng 4–50 ký tự gồm chữ, số, dấu chấm hoặc gạch dưới.")]
    public string TenDangNhap { get; set; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 8)]
    [DataType(DataType.Password)]
    public string MatKhau { get; set; } = string.Empty;

    [Required, Compare(nameof(MatKhau), ErrorMessage = "Mật khẩu nhập lại chưa khớp.")]
    [DataType(DataType.Password)]
    public string NhapLaiMatKhau { get; set; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 2)]
    public string HoTen { get; set; } = string.Empty;

    [Required, RegularExpression("^[0-9]{9,20}$")]
    public string SoDienThoai { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(150)]
    public string? Email { get; set; }

    [Required, RegularExpression("^[0-9]{12}$", ErrorMessage = "CCCD phải gồm 12 chữ số.")]
    public string CCCD { get; set; } = string.Empty;
}
