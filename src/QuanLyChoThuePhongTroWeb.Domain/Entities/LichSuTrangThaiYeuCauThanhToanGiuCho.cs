using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Domain.Entities
{
    [Table("lich_su_trang_thai_yeu_cau_thanh_toan_giu_cho")]
    public class LichSuTrangThaiYeuCauThanhToanGiuCho
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int LichSuTrangThaiYeuCauThanhToanGiuChoId { get; set; }

        public int YeuCauThanhToanGiuChoId { get; set; }
        [ForeignKey("YeuCauThanhToanGiuChoId")]
        public YeuCauThanhToanGiuCho YeuCauThanhToanGiuCho { get; set; }

        public TrangThaiYeuCauThanhToan? TrangThaiTruoc { get; set; }

        public TrangThaiYeuCauThanhToan TrangThaiSau { get; set; }

        public LoaiTacNhan LoaiTacNhan { get; set; }

        public int? NguoiThucHienId { get; set; }
        [ForeignKey("NguoiThucHienId")]
        public NguoiDung? NguoiThucHien { get; set; }

        public DateTime NgayThucHien { get; set; } = DateTime.UtcNow;

        [MaxLength(500)]
        public string? LyDo { get; set; }
    }
}
