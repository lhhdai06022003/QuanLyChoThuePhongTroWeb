using System.ComponentModel.DataAnnotations;

namespace QuanLyChoThuePhongTroWeb.Areas.KhachThue.ViewModels
{
    public class DoiMatKhauViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập mật khẩu hiện tại")]
        public string MatKhauCu { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập mật khẩu mới")]
        [MinLength(8, ErrorMessage = "Mật khẩu mới phải có ít nhất 8 ký tự")]
        [MaxLength(128, ErrorMessage = "Mật khẩu mới tối đa 128 ký tự")]
        public string MatKhauMoi { get; set; }

        [Required(ErrorMessage = "Vui lòng xác nhận mật khẩu mới")]
        [Compare("MatKhauMoi", ErrorMessage = "Mật khẩu xác nhận không khớp")]
        public string XacNhanMatKhau { get; set; }
    }
}
