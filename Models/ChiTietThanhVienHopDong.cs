using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuanLyChoThuePhongTroWeb.Models
{
    [Table("chi_tiet_thanh_vien_hop_dong")]
    public class ChiTietThanhVienHopDong
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ChiTietThanhVienHopDongId { get; set; }
        public int HopDongId { get; set; }
        [ForeignKey("HopDongId")]
        public HopDong HopDong { get; set; }
        public int NguoiThueId { get; set; }
        [ForeignKey("NguoiThueId")]
        public NguoiThue NguoiThue { get; set; }
        public DateTime NgayVao { get; set; } = DateTime.UtcNow;
        public DateTime? NgayChuyenDi { get; set; }
        public bool IsDeleted { get; set; } = false;
    }
}
