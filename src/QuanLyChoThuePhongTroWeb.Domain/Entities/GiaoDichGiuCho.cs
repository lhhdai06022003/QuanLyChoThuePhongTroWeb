using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Domain.Entities
{
    [Table("giao_dich_giu_cho")]
    public class GiaoDichGiuCho
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int GiaoDichGiuChoId { get; set; }

        public int YeuCauThanhToanGiuChoId { get; set; }
        [ForeignKey("YeuCauThanhToanGiuChoId")]
        public YeuCauThanhToanGiuCho YeuCauThanhToanGiuCho { get; set; }

        public int? MinhChungThanhToanGiuChoId { get; set; }
        [ForeignKey("MinhChungThanhToanGiuChoId")]
        public MinhChungThanhToanGiuCho? MinhChungThanhToanGiuCho { get; set; }

        [MaxLength(100)]
        public string MaGiaoDich { get; set; }

        public decimal SoTienThucNhan { get; set; }

        public PhuongThucThanhToan PhuongThuc { get; set; }

        public DateTime NgayThucNhan { get; set; }

        public int NguoiXacNhanId { get; set; }
        [ForeignKey("NguoiXacNhanId")]
        public NguoiDung NguoiXacNhan { get; set; }

        public DateTime NgayXacNhan { get; set; } = DateTime.UtcNow;

        [MaxLength(500)]
        public string? GhiChu { get; set; }
    }
}
