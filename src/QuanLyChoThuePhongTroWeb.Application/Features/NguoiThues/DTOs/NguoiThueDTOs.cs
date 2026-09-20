using System;
using System.ComponentModel.DataAnnotations;

namespace QuanLyChoThuePhongTroWeb.Application.Features.NguoiThues.DTOs
{
    public class NguoiThueReq
    {
        [Required(ErrorMessage = "Họ và tên không được để trống")]
        [StringLength(100, ErrorMessage = "Họ và tên không vượt quá 100 ký tự")]
        public string HoVaTen { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email là bắt buộc để cấp tài khoản đăng nhập")]
        [EmailAddress(ErrorMessage = "Email không đúng định dạng")]
        [StringLength(150, ErrorMessage = "Email không vượt quá 150 ký tự")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Số điện thoại không được để trống")]
        [RegularExpression(@"^(03|05|07|08|09)\d{8}$", ErrorMessage = "Số điện thoại không đúng định dạng Việt Nam")]
        public string SoDienThoai { get; set; } = string.Empty;

        [Required(ErrorMessage = "CCCD không được để trống")]
        [StringLength(12, MinimumLength = 9, ErrorMessage = "CCCD phải từ 9 đến 12 số")]
        public string CCCD { get; set; } = string.Empty;

        public DateTime? NgayCapCCCD { get; set; }
        public string? NoiCapCCCD { get; set; }
        public DateTime? NgaySinh { get; set; }
        public string? QueQuan { get; set; }
        public string? GhiChu { get; set; }
    }

    public class NguoiThueUpdateDto : NguoiThueReq
    {
        [Required]
        public int NguoiThueId { get; set; }
    }

    public class NguoiThueRes
    {
        public int NguoiThueId { get; set; }
        public string HoVaTen { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string SoDienThoai { get; set; } = string.Empty;
        public string CCCD { get; set; } = string.Empty;
        public DateTime? NgayCapCCCD { get; set; }
        public string? NoiCapCCCD { get; set; }
        public DateTime? NgaySinh { get; set; }
        public string? QueQuan { get; set; }
        public string? GhiChu { get; set; }
        public DateTime NgayTao { get; set; }
    }
}
