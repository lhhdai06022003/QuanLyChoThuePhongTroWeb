using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Domain.Entities
{
    [Table("yeu_cau_thanh_toan_giu_cho")]
    public class YeuCauThanhToanGiuCho
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int YeuCauThanhToanGiuChoId { get; set; }

        public int YeuCauGiuChoId { get; set; }
        [ForeignKey("YeuCauGiuChoId")]
        public YeuCauGiuCho YeuCauGiuCho { get; set; }

        [MaxLength(50)]
        public string MaYeuCau { get; set; }

        public decimal SoTien { get; set; }

        [MaxLength(100)]
        public string NoiDungChuyenKhoan { get; set; }

        public DateTime HanThanhToan { get; set; }

        public TrangThaiYeuCauThanhToan TrangThai { get; set; } = TrangThaiYeuCauThanhToan.ChoThanhToan;

        public int NguoiTaoId { get; set; }
        [ForeignKey("NguoiTaoId")]
        public NguoiDung NguoiTao { get; set; }

        public DateTime NgayTao { get; set; } = DateTime.UtcNow;

        public DateTime? NgayCapNhat { get; set; }

        public ICollection<MinhChungThanhToanGiuCho> MinhChungThanhToanGiuChos { get; set; } = new List<MinhChungThanhToanGiuCho>();

        public ICollection<GiaoDichGiuCho> GiaoDichGiuChos { get; set; } = new List<GiaoDichGiuCho>();

        public ICollection<LichSuTrangThaiYeuCauThanhToanGiuCho> LichSuTrangThais { get; set; } = new List<LichSuTrangThaiYeuCauThanhToanGiuCho>();
    }
}
