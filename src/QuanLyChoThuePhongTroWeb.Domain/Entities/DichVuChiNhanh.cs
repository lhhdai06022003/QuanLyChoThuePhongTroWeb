using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuanLyChoThuePhongTroWeb.Domain.Entities
{
    [Table("dich_vu_chi_nhanh")]
    public class DichVuChiNhanh
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int DichVuChiNhanhId { get; set; }

        public int ChiNhanhId { get; set; }
        [ForeignKey("ChiNhanhId")]
        public ChiNhanh ChiNhanh { get; set; }

        public int DichVuId { get; set; }
        [ForeignKey("DichVuId")]
        public DichVu DichVu { get; set; }

        public decimal GiaDichVu { get; set; }
        public bool MacDinh { get; set; } = false;

        public DateTime NgayTao { get; set; } = DateTime.UtcNow;
        public DateTime? NgayCapNhat { get; set; }
        public bool IsDeleted { get; set; } = false;

        public ICollection<DangKyDichVu> DangKyDichVus { get; set; }
    }
}
