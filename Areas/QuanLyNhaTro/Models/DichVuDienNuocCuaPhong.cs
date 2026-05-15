using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuanLyChoThuePhongTroWeb.Models
{
    [Table("dich_vu_dien_nuoc_cua_phong")]
    public class DichVuDienNuocCuaPhong
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int DichVuDienNuocCuaPhongId { get; set; }
        public int PhongTroId { get; set; }
        [ForeignKey("PhongTroId")]
        public PhongTro PhongTro { get; set; }
        public int Thang { get; set; }
        public int Nam { get; set; }
        public double ChiSoDienCu { get; set; }
        public double ChiSoDienMoi { get; set; }
        public double DonGiaDien { get; set; }

        public double ChiSoNuocCu { get; set; }
        public double ChiSoNuocMoi { get; set; }
        public double DonGiaNuoc { get; set; }

        public DateTime NgayTao { get; set; } = DateTime.UtcNow;
        public DateTime? NgayCapNhat { get; set; }
        public bool IsDeleted { get; set; } = false;
        public HoaDon HoaDon { get; set; }
        

    }
}
