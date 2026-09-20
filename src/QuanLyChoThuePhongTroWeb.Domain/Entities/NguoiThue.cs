using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuanLyChoThuePhongTroWeb.Domain.Entities
{
    [Table("nguoi_thue")]
    public class NguoiThue
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int NguoiThueId { get; set; }
        public string HoVaTen { get; set; }
        [Required(ErrorMessage = "Email là bắt buộc để cấp tài khoản đăng nhập")]
        [EmailAddress(ErrorMessage = "Email không đúng định dạng")]
        public string Email { get; set; }
        public string SoDienThoai { get; set; }
        public string CCCD { get; set; }
        public DateTime? NgayCapCCCD { get; set; }
        public string? NoiCapCCCD { get; set; }
        public DateTime? NgaySinh { get; set; }
        public string? QueQuan { get; set; }
        public string? GhiChu { get; set; }
        public DateTime NgayTao { get; set; } = DateTime.UtcNow;
        public DateTime? NgayCapNhat { get; set; }
        public bool IsDeleted { get; set; } = false;
        public NguoiDung? NguoiDung { get; set; }
        public ICollection<HopDong> HopDongs { get; set; }
        public ICollection<ChiTietThanhVienHopDong> ChiTietThanhVienHopDongs { get; set; }
    }
}
