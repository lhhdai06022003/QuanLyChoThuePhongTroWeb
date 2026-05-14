using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuanLyChoThuePhongTroWeb.Models
{
    [Table("dich_vu")]
    public class DichVu
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int DichVuId { get; set; }
        public int ChiNhanhId { get; set; }
        [ForeignKey("ChiNhanhId")]
        public ChiNhanh ChiNhanh { get; set; }
        public string TenDichVu { get; set; }
        public double GiaDichVu { get; set; }
        public string DonVi { get; set; }
        public string GhiChu { get; set; }
        public DateTime NgayTao { get; set; } = DateTime.UtcNow;
        public DateTime? NgayCapNhat { get; set; }
        public bool IsDeleted { get; set; } = false;
    }
}
