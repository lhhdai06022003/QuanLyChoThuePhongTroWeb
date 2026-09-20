using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuanLyChoThuePhongTroWeb.Domain.Entities
{
    [Table("chi_nhanh")]
    public class ChiNhanh
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ChiNhanhId { get; set; }
        [Required]
        [StringLength(10)]
        public string MaChiNhanh { get; set; }
        public string TenChiNhanh { get; set; }
        public string DiaChi { get; set; }
        public string MoTa { get; set; }
        public string SoDienThoai { get; set; }
        public DateTime NgayTao { get; set; } = DateTime.UtcNow;
        public DateTime? NgayCapNhat { get; set; }
        public bool IsDeleted { get; set; } = false;

        // Quan hệ
        public ICollection<PhongTro> PhongTros { get; set; }
        public ICollection<DichVuChiNhanh> DichVuChiNhanhs { get; set; }
    }
}
