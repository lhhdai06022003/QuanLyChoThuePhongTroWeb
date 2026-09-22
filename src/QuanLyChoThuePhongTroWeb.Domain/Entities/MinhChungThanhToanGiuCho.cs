using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Domain.Entities
{
    [Table("minh_chung_thanh_toan_giu_cho")]
    public class MinhChungThanhToanGiuCho
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int MinhChungThanhToanGiuChoId { get; set; }

        public int YeuCauThanhToanGiuChoId { get; set; }
        [ForeignKey("YeuCauThanhToanGiuChoId")]
        public YeuCauThanhToanGiuCho YeuCauThanhToanGiuCho { get; set; }

        [MaxLength(500)]
        public string UrlHinhAnh { get; set; }

        [MaxLength(200)]
        public string? PublicIdHinhAnh { get; set; }

        [MaxLength(100)]
        public string? MaGiaoDichNganHang { get; set; }

        public decimal SoTienKhaiBao { get; set; }

        public DateTime NgayChuyenTien { get; set; }

        [MaxLength(500)]
        public string? GhiChuKhachHang { get; set; }

        public TrangThaiMinhChungThanhToan TrangThai { get; set; } = TrangThaiMinhChungThanhToan.ChoXacNhan;

        public int? NguoiDoiChieuId { get; set; }
        [ForeignKey("NguoiDoiChieuId")]
        public NguoiDung? NguoiDoiChieu { get; set; }

        public DateTime? NgayDoiChieu { get; set; }

        [MaxLength(500)]
        public string? LyDoTuChoi { get; set; }

        public DateTime NgayTao { get; set; } = DateTime.UtcNow;
    }
}
