using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Domain.Entities
{
    [Table("lich_su_trang_thai_hoa_don")]
    public class LichSuTrangThaiHoaDon
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int LichSuTrangThaiHoaDonId { get; set; }

        public int HoaDonId { get; set; }
        [ForeignKey("HoaDonId")]
        public HoaDon HoaDon { get; set; }

        public TrangThaiPhatHanhHoaDon? TrangThaiPhatHanhCu { get; set; }
        public TrangThaiPhatHanhHoaDon TrangThaiPhatHanhMoi { get; set; }

        public TrangThaiHoaDon? TrangThaiThanhToanCu { get; set; }
        public TrangThaiHoaDon TrangThaiThanhToanMoi { get; set; }

        public int? NguoiThucHienId { get; set; }
        public DateTime NgayThucHien { get; set; } = DateTime.UtcNow;
        public string? LyDo { get; set; }
    }
}
