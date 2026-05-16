using System.ComponentModel.DataAnnotations;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels
{
    public class NguoiThueReq
    {
        [Required(ErrorMessage = "Họ và tên không được để trống")]
        [StringLength(100, ErrorMessage = "Họ và tên không vượt quá 100 ký tự")]
        public string HoVaTen { get; set; }

        [Required(ErrorMessage = "Số điện thoại không được để trống")]
        [RegularExpression(@"^(03|05|07|08|09)\d{8}$", ErrorMessage = "Số điện thoại không đúng định dạng Việt Nam")]
        public string SoDienThoai { get; set; }

        [Required(ErrorMessage = "CCCD không được để trống")]
        [StringLength(12, MinimumLength = 9, ErrorMessage = "CCCD phải từ 9 đến 12 số")]
        public string CCCD { get; set; }

        public DateTime? NgayCapCCCD { get; set; }
        public string NoiCapCCCD { get; set; }
        public DateTime? NgaySinh { get; set; }
        public string QueQuan { get; set; } // Sửa lỗi chính tả "QueQUan" trong model gốc
        public string GhiChu { get; set; }
    }
    public class NguoiThueUpdateDto : NguoiThueReq
    {
        [Required]
        public int NguoiThueId { get; set; }
    }
}
