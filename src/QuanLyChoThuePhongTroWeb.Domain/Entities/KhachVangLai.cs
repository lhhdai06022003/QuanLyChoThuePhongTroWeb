using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuanLyChoThuePhongTroWeb.Domain.Entities
{
    [Table("khach_vang_lai")]
    public class KhachVangLai
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int KhachVangLaiId { get; set; }

        public int NguoiDungId { get; set; }
        [ForeignKey("NguoiDungId")]
        public NguoiDung NguoiDung { get; set; }

        [MaxLength(100)]
        public string HoTen { get; set; }

        [MaxLength(150)]
        public string? Email { get; set; }

        [MaxLength(150)]
        public string? EmailNormalized { get; set; }

        [MaxLength(20)]
        public string SoDienThoai { get; set; }

        public bool DaXacMinhEmail { get; set; } = false;

        public DateTime NgayTao { get; set; } = DateTime.UtcNow;

        public DateTime? NgayCapNhat { get; set; }
    }
}
