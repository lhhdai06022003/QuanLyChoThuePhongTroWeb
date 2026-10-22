using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Domain.Entities
{
    [Table("lich_su_trang_thai_yeu_cau_thanh_toan_hoa_don")]
    public class LichSuTrangThaiYeuCauThanhToanHoaDon
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int LichSuTrangThaiYeuCauThanhToanHoaDonId { get; set; }

        public int YeuCauThanhToanHoaDonId { get; set; }
        [ForeignKey("YeuCauThanhToanHoaDonId")]
        public YeuCauThanhToanHoaDon YeuCauThanhToanHoaDon { get; set; }

        public TrangThaiYeuCauThanhToan? TrangThaiCu { get; set; }
        public TrangThaiYeuCauThanhToan TrangThaiMoi { get; set; }

        public int? NguoiThucHienId { get; set; }
        public DateTime NgayThucHien { get; set; } = DateTime.UtcNow;
        public string? LyDo { get; set; }
    }
}
