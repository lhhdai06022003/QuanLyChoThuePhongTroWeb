using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuanLyChoThuePhongTroWeb.Models
{
    [Table("chi_tiet_hoa_don")]
    public class ChiTietHoaDon
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ChiTietHoaDonId { get; set; }
        public int HoaDonId { get; set; }
        [ForeignKey("HoaDonId")]
        public HoaDon HoaDon { get; set; }

        public string TenDichVu { get; set; }
        public double DonGia { get; set; }
        public int SoLuong { get; set; }
        public double TongTien { get; set; }
        public bool IsDeleted { get; set; } = false;
        public int? DichVuId { get; set; }
        [ForeignKey("DichVuId")]
        public DichVu DichVu { get; set; }
    }
}
