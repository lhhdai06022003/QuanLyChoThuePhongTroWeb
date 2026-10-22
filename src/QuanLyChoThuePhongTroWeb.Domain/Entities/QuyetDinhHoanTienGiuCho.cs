using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Domain.Entities
{
    [Table("quyet_dinh_hoan_tien_giu_cho")]
    public class QuyetDinhHoanTienGiuCho
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int QuyetDinhHoanTienGiuChoId { get; set; }

        public int YeuCauGiuChoId { get; set; }
        [ForeignKey("YeuCauGiuChoId")]
        public YeuCauGiuCho YeuCauGiuCho { get; set; }

        public decimal SoTienHoanDuyet { get; set; }

        [MaxLength(500)]
        public string LyDo { get; set; }

        public int NguoiQuyetDinhId { get; set; }
        [ForeignKey("NguoiQuyetDinhId")]
        public NguoiDung NguoiQuyetDinh { get; set; }

        public DateTime NgayQuyetDinh { get; set; } = DateTime.UtcNow;

        public TrangThaiHoanTien TrangThai { get; set; } = TrangThaiHoanTien.ChoHoanTien;

        [MaxLength(500)]
        public string? GhiChu { get; set; }

        public ICollection<GiaoDichHoanTienGiuCho> GiaoDichHoanTiens { get; set; } = new List<GiaoDichHoanTienGiuCho>();
    }
}
