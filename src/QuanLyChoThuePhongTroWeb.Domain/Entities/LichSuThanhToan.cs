using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Domain.Entities
{
    [Table("lich_su_thanh_toan")]
    public class LichSuThanhToan
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int LichSuThanhToanId { get; set; }
        public int HoaDonId { get; set; }
        [ForeignKey("HoaDonId")]
        public HoaDon HoaDon { get; set; }
        public string MaGiaoDich { get; set; } = string.Empty;
        public int? NguoiXacNhanId { get; set; }
        [ForeignKey("NguoiXacNhanId")]
        public NguoiDung? NguoiXacNhan { get; set; }
        public decimal SoTienThanhToan { get; set; }
        public PhuongThucThanhToan PhuongThucThanhToan { get; set; }
        public DateTime NgayThanhToan { get; set; } = DateTime.UtcNow;
        public DateTime? NgayXacNhan { get; set; }
        public string GhiChu { get; set; } = string.Empty;
        public bool IsDeleted { get; set; } = false;

        public int? MinhChungThanhToanHoaDonId { get; set; }
        [ForeignKey("MinhChungThanhToanHoaDonId")]
        public MinhChungThanhToanHoaDon? MinhChungThanhToanHoaDon { get; set; }
    }
}
