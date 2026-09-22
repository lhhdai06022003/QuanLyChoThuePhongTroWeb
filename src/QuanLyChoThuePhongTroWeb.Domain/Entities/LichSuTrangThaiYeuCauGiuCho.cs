using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Domain.Entities
{
    [Table("lich_su_trang_thai_yeu_cau_giu_cho")]
    public class LichSuTrangThaiYeuCauGiuCho
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int LichSuTrangThaiYeuCauGiuChoId { get; set; }

        public int YeuCauGiuChoId { get; set; }
        [ForeignKey("YeuCauGiuChoId")]
        public YeuCauGiuCho YeuCauGiuCho { get; set; }

        public TrangThaiYeuCauGiuCho? TrangThaiTruoc { get; set; }

        public TrangThaiYeuCauGiuCho TrangThaiSau { get; set; }

        public LoaiTacNhan LoaiTacNhan { get; set; }

        public int? NguoiThucHienId { get; set; }
        [ForeignKey("NguoiThucHienId")]
        public NguoiDung? NguoiThucHien { get; set; }

        public DateTime NgayThucHien { get; set; } = DateTime.UtcNow;

        [MaxLength(500)]
        public string? LyDo { get; set; }
    }
}
