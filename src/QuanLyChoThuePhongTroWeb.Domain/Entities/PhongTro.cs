using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuanLyChoThuePhongTroWeb.Domain.Entities
{
    [Table("phong_tro")]
    public class PhongTro
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int PhongTroId { get; set; }
        public int ChiNhanhId { get; set; }
        [ForeignKey("ChiNhanhId")]
        public ChiNhanh ChiNhanh { get; set; }
        public string SoPhong { get; set; }
        public int TangLau { get; set; }
        public double GiaThue { get; set; }
        public double DienTich { get; set; }
        public int SoNguoiToiDa { get; set; }
        public TrangThaiPhong TrangThai { get; set; }
        public string MoTa { get; set; }
        public DateTime NgayTao { get; set; } = DateTime.UtcNow;
        public DateTime? NgayCapNhat { get; set; }
        public bool IsDeleted { get; set; } = false;
        public ICollection<HopDong> HopDongs { get; set; }
        public ICollection<DangKyDichVu> DangKyDichVus { get; set; }
    }
}
