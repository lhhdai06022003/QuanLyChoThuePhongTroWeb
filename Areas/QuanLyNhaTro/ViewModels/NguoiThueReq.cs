using System.ComponentModel.DataAnnotations;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels
{
    public class NguoiThueReq
    {
        [Required(ErrorMessage = "Họ và tên không được để trống")]
        [StringLength(100, ErrorMessage = "Họ và tên không vượt quá 100 ký tự")]
        public string HoVaTen { get; set; }

        // --- XỬ LÝ RIÊNG CHO EMAIL ĐỂ TRÁNH LỖI ĐỊNH DẠNG KHI ĐỂ TRỐNG ---
        private string? _email;

        [EmailAddress(ErrorMessage = "Email không đúng định dạng")]
        [StringLength(150, ErrorMessage = "Email không vượt quá 150 ký tự")]
        public string? Email
        {
            get => _email;
            // Nếu Client gửi chuỗi rỗng "" hoặc khoảng trắng, tự động chuyển về null để qua bộ lọc [EmailAddress]
            set => _email = string.IsNullOrWhiteSpace(value) ? null : value;
        }
        // -----------------------------------------------------------------

        [Required(ErrorMessage = "Số điện thoại không được để trống")]
        [RegularExpression(@"^(03|05|07|08|09)\d{8}$", ErrorMessage = "Số điện thoại không đúng định dạng Việt Nam")]
        public string SoDienThoai { get; set; }

        [Required(ErrorMessage = "CCCD không được để trống")]
        [StringLength(12, MinimumLength = 9, ErrorMessage = "CCCD phải từ 9 đến 12 số")]
        public string CCCD { get; set; }

        public DateTime? NgayCapCCCD { get; set; }

        // Các trường này để string? thông thường là được vì không bị vướng bộ lọc định dạng nào
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
}
