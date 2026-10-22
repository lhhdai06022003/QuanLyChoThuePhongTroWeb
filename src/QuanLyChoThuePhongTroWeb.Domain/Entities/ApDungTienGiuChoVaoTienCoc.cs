using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuanLyChoThuePhongTroWeb.Domain.Entities
{
    [Table("ap_dung_tien_giu_cho_vao_tien_coc")]
    public class ApDungTienGiuChoVaoTienCoc
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ApDungTienGiuChoVaoTienCocId { get; set; }

        public int YeuCauGiuChoId { get; set; }
        [ForeignKey("YeuCauGiuChoId")]
        public YeuCauGiuCho YeuCauGiuCho { get; set; }

        public int HopDongId { get; set; }
        [ForeignKey("HopDongId")]
        public HopDong HopDong { get; set; }

        public decimal SoTienApDung { get; set; }

        public DateTime NgayApDung { get; set; } = DateTime.UtcNow;

        public int NguoiThucHienId { get; set; }
        [ForeignKey("NguoiThucHienId")]
        public NguoiDung NguoiThucHien { get; set; }

        [MaxLength(500)]
        public string? GhiChu { get; set; }
    }
}
