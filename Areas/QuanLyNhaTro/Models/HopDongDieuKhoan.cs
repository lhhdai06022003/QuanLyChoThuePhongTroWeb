using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuanLyChoThuePhongTroWeb.Models
{
    [Table("hop_dong_dieu_khoan")]
    public class HopDongDieuKhoan
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public int HopDongId { get; set; }
        [ForeignKey("HopDongId")]
        public HopDong HopDong { get; set; }

        [Required]
        [MaxLength(255)]
        public string TieuDe { get; set; }

        [Required]
        public string NoiDung { get; set; }

        public int ThuTu { get; set; }
    }
}
