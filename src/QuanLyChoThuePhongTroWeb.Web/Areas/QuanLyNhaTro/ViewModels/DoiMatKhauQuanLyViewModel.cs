using System.ComponentModel.DataAnnotations;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels
{
    // Màn đổi mật khẩu cổng quản lý (spec bảo mật xác thực §2.5). BatBuoc = đang bị ép đổi mật khẩu mặc định.
    public sealed record DoiMatKhauPageModel(bool BatBuoc);

    // Độ dài kiểm ở đây để báo sớm; phần còn lại của chính sách (trùng tên, danh sách yếu) do service kiểm.
    public class DoiMatKhauQuanLyReq
    {
        [Required(ErrorMessage = "Vui lòng nhập mật khẩu hiện tại")]
        public string MatKhauHienTai { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập mật khẩu mới")]
        [MinLength(8, ErrorMessage = "Mật khẩu mới phải có ít nhất 8 ký tự")]
        [MaxLength(128, ErrorMessage = "Mật khẩu mới tối đa 128 ký tự")]
        public string MatKhauMoi { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng xác nhận mật khẩu mới")]
        public string XacNhanMatKhau { get; set; } = string.Empty;
    }
}
