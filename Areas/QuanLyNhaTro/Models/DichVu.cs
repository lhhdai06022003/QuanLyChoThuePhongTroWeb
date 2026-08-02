using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuanLyChoThuePhongTroWeb.Models
{
    [Table("dich_vu")]
    public class DichVu
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int DichVuId { get; set; }

        public string TenDichVu { get; set; }
        public string DonVi { get; set; }
        public string GhiChu { get; set; }
        public DateTime NgayTao { get; set; } = DateTime.UtcNow;
        public DateTime? NgayCapNhat { get; set; }
        public bool IsDeleted { get; set; } = false;
        public LoaiDichVu LoaiDichVu { get; set; } = LoaiDichVu.Khac;

        // Quan hệ hướng tới bảng trung gian
        public ICollection<DichVuChiNhanh> DichVuChiNhanhs { get; set; }
    }

    public enum LoaiDichVu
    {
        Khac = 0,
        Dien = 1,
        Nuoc = 2
    }
}
