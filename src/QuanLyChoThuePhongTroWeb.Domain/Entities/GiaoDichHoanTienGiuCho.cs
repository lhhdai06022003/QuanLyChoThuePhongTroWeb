using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuanLyChoThuePhongTroWeb.Domain.Entities
{
    [Table("giao_dich_hoan_tien_giu_cho")]
    public class GiaoDichHoanTienGiuCho
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int GiaoDichHoanTienGiuChoId { get; set; }

        public int QuyetDinhHoanTienGiuChoId { get; set; }
        [ForeignKey("QuyetDinhHoanTienGiuChoId")]
        public QuyetDinhHoanTienGiuCho QuyetDinhHoanTienGiuCho { get; set; }

        public decimal SoTienHoan { get; set; }

        [MaxLength(100)]
        public string MaGiaoDichHoan { get; set; }

        [MaxLength(500)]
        public string? UrlHinhAnhMinhChung { get; set; }

        public int NguoiXacNhanId { get; set; }
        [ForeignKey("NguoiXacNhanId")]
        public NguoiDung NguoiXacNhan { get; set; }

        public DateTime NgayHoan { get; set; } = DateTime.UtcNow;

        [MaxLength(500)]
        public string? GhiChu { get; set; }
    }
}
