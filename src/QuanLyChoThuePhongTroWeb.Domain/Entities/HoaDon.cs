using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuanLyChoThuePhongTroWeb.Domain.Entities
{
    [Table("hoa_don")]
    public class HoaDon
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int HoaDonId { get; set; }
        public string MaHoaDon { get; set; }
        public int HopDongId { get; set; }
        [ForeignKey("HopDongId")]
        public HopDong HopDong { get; set; }
        public int Thang { get; set; }
        public int Nam { get; set; }
        public double TongTien { get; set; }
        public TrangThaiHoaDon TrangThaiHoaDon { get; set; }
        public DateTime NgayTao { get; set; } = DateTime.UtcNow;
        public DateTime? NgayCapNhat { get; set; }
        public bool IsDeleted { get; set; } = false;
        // Liên kết 1-1 với điện nước (Nullable để có thể xuất hóa đơn khác)
        public int? DichVuDienNuocCuaPhongId { get; set; }
        [ForeignKey("DichVuDienNuocCuaPhongId")]
        public DichVuDienNuocCuaPhong DichVuDienNuocCuaPhong { get; set; }

        public ICollection<ChiTietHoaDon> ChiTietHoaDonDichVus { get; set; }
        public ICollection<LichSuThanhToan> LichSuThanhToans { get; set; }
    }
}
