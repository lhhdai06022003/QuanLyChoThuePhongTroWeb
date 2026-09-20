using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuanLyChoThuePhongTroWeb.Domain.Entities
{
    [Table("nguoi_dung")]
    public class NguoiDung
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int NguoiDungId { get; set; }
        public string TenDangNhap { get; set; }
        public string MatKhauHash { get; set; }
        public bool IsActive { get; set; } = true;
        public Role Role { get; set; }
        public DateTime NgayTao { get; set; } = DateTime.UtcNow;
        public DateTime? LanDangNhapCuoi { get; set; }
        public int? NguoiThueId { get; set; }
        [ForeignKey("NguoiThueId")]
        public NguoiThue? NguoiThue { get; set; }
        public bool IsDeleted { get; set; } = false;
    }
}
