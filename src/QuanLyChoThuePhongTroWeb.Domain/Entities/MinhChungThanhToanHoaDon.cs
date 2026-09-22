using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Domain.Entities
{
    [Table("minh_chung_thanh_toan_hoa_don")]
    public class MinhChungThanhToanHoaDon
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int MinhChungThanhToanHoaDonId { get; set; }

        public int YeuCauThanhToanHoaDonId { get; set; }
        [ForeignKey("YeuCauThanhToanHoaDonId")]
        public YeuCauThanhToanHoaDon YeuCauThanhToanHoaDon { get; set; }

        public string HinhAnhUrl { get; set; }
        public string? PublicId { get; set; }
        public string? MaGiaoDichNganHang { get; set; }
        public decimal SoTienKhaiBao { get; set; }
        public DateTime NgayChuyenKhaiBao { get; set; }
        public TrangThaiMinhChungThanhToan TrangThaiDoiChieu { get; set; } = TrangThaiMinhChungThanhToan.ChoXacNhan;

        public int? NguoiDoiChieuId { get; set; }
        public DateTime? NgayDoiChieu { get; set; }
        public string? LyDoTuChoi { get; set; }

        public DateTime NgayTao { get; set; } = DateTime.UtcNow;
        public bool IsDeleted { get; set; } = false;

        public LichSuThanhToan? LichSuThanhToan { get; set; }
    }
}
