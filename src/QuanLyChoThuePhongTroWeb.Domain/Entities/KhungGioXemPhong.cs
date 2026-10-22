using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuanLyChoThuePhongTroWeb.Domain.Entities
{
    [Table("khung_gio_xem_phong")]
    public class KhungGioXemPhong
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int KhungGioXemPhongId { get; set; }

        public int PhongTroId { get; set; }
        [ForeignKey("PhongTroId")]
        public PhongTro PhongTro { get; set; }

        public DateTime ThoiGianBatDau { get; set; }

        public DateTime ThoiGianKetThuc { get; set; }

        public int SoLuongToiDa { get; set; }

        public int? NguoiPhuTrachId { get; set; }
        [ForeignKey("NguoiPhuTrachId")]
        public NguoiDung? NguoiPhuTrach { get; set; }

        public bool IsActive { get; set; } = true;

        public int NguoiTaoId { get; set; }
        [ForeignKey("NguoiTaoId")]
        public NguoiDung NguoiTao { get; set; }

        public DateTime NgayTao { get; set; } = DateTime.UtcNow;

        public DateTime? NgayCapNhat { get; set; }

        public ICollection<YeuCauXemPhong> YeuCauXemPhongs { get; set; } = new List<YeuCauXemPhong>();
    }
}
