using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Domain.Entities
{
    [Table("yeu_cau_giu_cho")]
    public class YeuCauGiuCho
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int YeuCauGiuChoId { get; set; }

        public int KhachVangLaiId { get; set; }
        [ForeignKey("KhachVangLaiId")]
        public KhachVangLai KhachVangLai { get; set; }

        public int? YeuCauXemPhongId { get; set; }
        [ForeignKey("YeuCauXemPhongId")]
        public YeuCauXemPhong? YeuCauXemPhong { get; set; }

        public int PhongTroId { get; set; }
        [ForeignKey("PhongTroId")]
        public PhongTro PhongTro { get; set; }

        public decimal? SoTienGiuCho { get; set; }

        public DateTime? HanThanhToan { get; set; }

        public DateTime? HanKyHopDong { get; set; }

        public int? NguoiDuyetId { get; set; }
        [ForeignKey("NguoiDuyetId")]
        public NguoiDung? NguoiDuyet { get; set; }

        public DateTime? NgayDuyet { get; set; }

        public TrangThaiYeuCauGiuCho TrangThai { get; set; } = TrangThaiYeuCauGiuCho.MoiTao;

        [MaxLength(500)]
        public string? LyDoTuChoiHoacHuy { get; set; }

        public DateTime NgayTao { get; set; } = DateTime.UtcNow;

        public DateTime? NgayCapNhat { get; set; }

        public ICollection<LichSuTrangThaiYeuCauGiuCho> LichSuTrangThais { get; set; } = new List<LichSuTrangThaiYeuCauGiuCho>();

        public ICollection<YeuCauThanhToanGiuCho> YeuCauThanhToanGiuChos { get; set; } = new List<YeuCauThanhToanGiuCho>();

        public ApDungTienGiuChoVaoTienCoc? ApDungTienGiuChoVaoTienCoc { get; set; }

        public QuyetDinhHoanTienGiuCho? QuyetDinhHoanTienGiuCho { get; set; }
    }
}
