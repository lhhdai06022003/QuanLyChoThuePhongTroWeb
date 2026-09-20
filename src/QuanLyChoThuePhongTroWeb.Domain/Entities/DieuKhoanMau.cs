using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuanLyChoThuePhongTroWeb.Domain.Entities
{
    [Table("dieu_khoan_mau")]
    public class DieuKhoanMau
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int DieuKhoanMauId { get; set; }

        [Required]
        [MaxLength(255)]
        public string TieuDe { get; set; }

        [Required]
        public string NoiDung { get; set; }

        public bool IsDeleted { get; set; } = false;
        
        public DateTime NgayTao { get; set; } = DateTime.UtcNow;
        public DateTime? NgayCapNhat { get; set; }
    }
}
