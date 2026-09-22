using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Domain.Entities
{
    [Table("yeu_cau_xem_phong")]
    public class YeuCauXemPhong
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int YeuCauXemPhongId { get; set; }

        public int PhongTroId { get; set; }
        [ForeignKey("PhongTroId")]
        public PhongTro PhongTro { get; set; }

        public int? KhachVangLaiId { get; set; }
        [ForeignKey("KhachVangLaiId")]
        public KhachVangLai? KhachVangLai { get; set; }

        public int? KhungGioXemPhongId { get; set; }
        [ForeignKey("KhungGioXemPhongId")]
        public KhungGioXemPhong? KhungGioXemPhong { get; set; }

        [MaxLength(100)]
        public string HoTen { get; set; }

        [MaxLength(20)]
        public string SoDienThoai { get; set; }

        [MaxLength(150)]
        public string? Email { get; set; }

        public DateTime ThoiGianMongMuon { get; set; }

        public DateTime? ThoiGianXacNhan { get; set; }

        public NguonTaoYeuCauXemPhong NguonTao { get; set; }

        public HinhThucXacNhanLich? HinhThucXacNhan { get; set; }

        public TrangThaiYeuCauXemPhong TrangThai { get; set; } = TrangThaiYeuCauXemPhong.ChoChonLich;

        [MaxLength(1000)]
        public string? GhiChu { get; set; }

        public DateTime NgayTao { get; set; } = DateTime.UtcNow;

        public DateTime? NgayCapNhat { get; set; }

        public ICollection<LichSuTrangThaiYeuCauXemPhong> LichSuTrangThais { get; set; } = new List<LichSuTrangThaiYeuCauXemPhong>();
    }
}
