using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuanLyChoThuePhongTroWeb.Domain.Entities
{
    [Table("dich_vu")]
    public class DichVu
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int DichVuId { get; set; }
        public string TenDichVu { get; set; }
        public string DonVi { get; set; }
        public string GhiChu { get; set; } = string.Empty;
        public DateTime NgayTao { get; set; } = DateTime.UtcNow;
        public DateTime? NgayCapNhat { get; set; }
        public bool IsDeleted { get; set; } = false;

        // Phân loại dịch vụ (Khác, Điện, Nước)
        public LoaiDichVu LoaiDichVu { get; set; } = LoaiDichVu.Khac;

        // Quan hệ hướng tới bảng trung gian
        public ICollection<DichVuChiNhanh> DichVuChiNhanhs { get; set; }
    }
}
