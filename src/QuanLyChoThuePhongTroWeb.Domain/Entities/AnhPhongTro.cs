using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuanLyChoThuePhongTroWeb.Domain.Entities
{
    [Table("anh_phong_tro")]
    public class AnhPhongTro
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int AnhPhongTroId { get; set; }

        public int PhongTroId { get; set; }
        [ForeignKey("PhongTroId")]
        public PhongTro PhongTro { get; set; }

        [MaxLength(500)]
        public string Url { get; set; }

        [MaxLength(200)]
        public string PublicId { get; set; }

        public int ThuTuHienThi { get; set; }

        public bool LaAnhDaiDien { get; set; } = false;

        [MaxLength(255)]
        public string? ChuThich { get; set; }

        public bool IsActive { get; set; } = true;

        public int NguoiTaiLenId { get; set; }
        [ForeignKey("NguoiTaiLenId")]
        public NguoiDung NguoiTaiLen { get; set; }

        public DateTime NgayTaiLen { get; set; } = DateTime.UtcNow;
    }
}
