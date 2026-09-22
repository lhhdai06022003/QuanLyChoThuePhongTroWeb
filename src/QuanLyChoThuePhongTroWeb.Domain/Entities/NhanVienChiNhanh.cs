using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuanLyChoThuePhongTroWeb.Domain.Entities
{
    [Table("nhan_vien_chi_nhanh")]
    public class NhanVienChiNhanh
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int NhanVienChiNhanhId { get; set; }

        public int NguoiDungId { get; set; }
        [ForeignKey("NguoiDungId")]
        public NguoiDung NguoiDung { get; set; }

        public int ChiNhanhId { get; set; }
        [ForeignKey("ChiNhanhId")]
        public ChiNhanh ChiNhanh { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime NgayPhanCong { get; set; } = DateTime.UtcNow;

        public int NguoiPhanCongId { get; set; }
        [ForeignKey("NguoiPhanCongId")]
        public NguoiDung NguoiPhanCong { get; set; }

        public DateTime? NgayThuHoi { get; set; }

        public int? NguoiThuHoiId { get; set; }
        [ForeignKey("NguoiThuHoiId")]
        public NguoiDung? NguoiThuHoi { get; set; }

        [MaxLength(500)]
        public string? LyDo { get; set; }
    }
}
