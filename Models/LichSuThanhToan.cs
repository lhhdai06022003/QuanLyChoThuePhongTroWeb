using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuanLyChoThuePhongTroWeb.Models
{
    [Table("lich_su_thanh_toan")]
    public class LichSuThanhToan
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int LichSuThanhToanId { get; set; }
        public string MaGiaoDich { get; set; }
        public int HoaDonId { get; set; }
        [ForeignKey("HoaDonId")]
        public HoaDon HoaDon { get; set; }
        // Người thực hiện thu tiền (Admin hoặc Nhân viên)
        public int? NguoiXacNhanId { get; set; }
        [ForeignKey("NguoiXacNhanId")]
        public NguoiDung NguoiXacNhan { get; set; }
        public double SoTienThanhToan { get; set; }
        public PhuongThucThanhToan PhuongThucThanhToan { get; set; }
        public DateTime NgayThanhToan { get; set; } = DateTime.UtcNow;
        public string GhiChu { get; set; }
        public bool IsDeleted { get; set; } = false;
    }
    public enum PhuongThucThanhToan
    {
        [Description("Tiền mặt")]
        TienMat = 0,

        [Description("Chuyển khoản")]
        ChuyenKhoan = 1
    }
}
