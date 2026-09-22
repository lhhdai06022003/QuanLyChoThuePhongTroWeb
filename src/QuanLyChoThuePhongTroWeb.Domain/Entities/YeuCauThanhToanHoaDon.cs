using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Domain.Entities
{
    [Table("yeu_cau_thanh_toan_hoa_don")]
    public class YeuCauThanhToanHoaDon
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int YeuCauThanhToanHoaDonId { get; set; }

        public int HoaDonId { get; set; }
        [ForeignKey("HoaDonId")]
        public HoaDon HoaDon { get; set; }

        public string MaYeuCau { get; set; }
        public decimal SoTien { get; set; }
        public string NoiDungChuyenKhoan { get; set; }
        public DateTime? HanThanhToan { get; set; }
        public TrangThaiYeuCauThanhToan TrangThai { get; set; } = TrangThaiYeuCauThanhToan.ChoThanhToan;

        public int? NguoiTaoId { get; set; }
        public DateTime NgayTao { get; set; } = DateTime.UtcNow;
        public DateTime? NgayCapNhat { get; set; }
        public bool IsDeleted { get; set; } = false;

        public ICollection<MinhChungThanhToanHoaDon> MinhChungThanhToanHoaDons { get; set; } = new List<MinhChungThanhToanHoaDon>();
        public ICollection<LichSuTrangThaiYeuCauThanhToanHoaDon> LichSuTrangThaiYeuCauThanhToanHoaDons { get; set; } = new List<LichSuTrangThaiYeuCauThanhToanHoaDon>();
    }
}
