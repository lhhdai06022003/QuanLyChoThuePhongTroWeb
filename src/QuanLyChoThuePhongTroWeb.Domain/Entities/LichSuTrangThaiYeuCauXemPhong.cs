using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Domain.Entities
{
    [Table("lich_su_trang_thai_yeu_cau_xem_phong")]
    public class LichSuTrangThaiYeuCauXemPhong
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int LichSuTrangThaiYeuCauXemPhongId { get; set; }

        public int YeuCauXemPhongId { get; set; }
        [ForeignKey("YeuCauXemPhongId")]
        public YeuCauXemPhong YeuCauXemPhong { get; set; }

        public TrangThaiYeuCauXemPhong? TrangThaiTruoc { get; set; }

        public TrangThaiYeuCauXemPhong TrangThaiSau { get; set; }

        public LoaiTacNhan LoaiTacNhan { get; set; }

        public int? NguoiThucHienId { get; set; }
        [ForeignKey("NguoiThucHienId")]
        public NguoiDung? NguoiThucHien { get; set; }

        public DateTime NgayThucHien { get; set; } = DateTime.UtcNow;

        [MaxLength(500)]
        public string? LyDo { get; set; }
    }
}
