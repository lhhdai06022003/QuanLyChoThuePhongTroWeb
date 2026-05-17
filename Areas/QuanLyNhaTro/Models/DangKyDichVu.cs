using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuanLyChoThuePhongTroWeb.Models
{
    [Table("dang_ky_dich_vu")]
    public class DangKyDichVu
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int DangKyDichVuId { get; set; }
        public int PhongTroId { get; set; }
        [ForeignKey("PhongTroId")]
        public PhongTro PhongTro { get; set; }

        public int DichVuChiNhanhId { get; set; }
        [ForeignKey("DichVuChiNhanhId")]
        public DichVuChiNhanh DichVuChiNhanh { get; set; }

        public int SoLuong { get; set; }
    }
}
