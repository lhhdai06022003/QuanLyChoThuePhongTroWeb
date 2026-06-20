using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuanLyChoThuePhongTroWeb.Models
{
    [Table("hop_dong")]
    public class HopDong
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int HopDongId { get; set; }
        public string MaHopDong { get; set; }
        public int PhongTroId { get; set; }
        [ForeignKey("PhongTroId")]
        public PhongTro PhongTro { get; set; }

        public int NguoiThueId { get; set; }
        [ForeignKey("NguoiThueId")]
        public NguoiThue NguoiThue { get; set; }

        public DateTime ThoiDiemBatDau { get; set; }
        public DateTime? ThoiDiemKetThuc { get; set; }
        public double TienCocPhong { get; set; }
        public double TienThuePhong { get; set; }
        public TrangThaiHopDong TrangThaiHopDong { get; set; }
        public DateTime NgayTao { get; set; } = DateTime.UtcNow;
        public DateTime? NgayCapNhat { get; set; }
        public bool IsDeleted { get; set; } = false;

        public ICollection<HoaDon> HoaDons { get; set; }
        public ICollection<ChiTietThanhVienHopDong> ChiTietThanhVienHopDongs { get; set; }
        public ICollection<HopDongDieuKhoan> HopDongDieuKhoans { get; set; }
    }
    public enum TrangThaiHopDong
    {
        [Description("Đang hoạt động")]
        DangHoatDong = 1,

        [Description("Đã kết thúc")]
        DaKetThuc = 2,

        [Description("Đã hủy")]
        DaHuy = 3
    }
}
