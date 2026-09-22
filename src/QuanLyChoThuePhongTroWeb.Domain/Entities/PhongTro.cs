using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuanLyChoThuePhongTroWeb.Domain.Entities
{
    [Table("phong_tro")]
    public class PhongTro
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int PhongTroId { get; set; }
        public int ChiNhanhId { get; set; }
        [ForeignKey("ChiNhanhId")]
        public ChiNhanh ChiNhanh { get; set; }
        public string SoPhong { get; set; }
        public int TangLau { get; set; }
        public decimal GiaThue { get; set; }
        public double DienTich { get; set; }
        public int SoNguoiToiDa { get; set; }
        public TrangThaiPhong TrangThai { get; set; }
        public string MoTa { get; set; }
        public DateTime NgayTao { get; set; } = DateTime.UtcNow;
        public DateTime? NgayCapNhat { get; set; }
        public bool IsDeleted { get; set; } = false;
        public bool DuocDangTin { get; set; } = false;
        public string? TieuDeDangTin { get; set; }
        public string? MaCongKhai { get; set; }
        public int? NguoiDangTinId { get; set; }
        [ForeignKey("NguoiDangTinId")]
        public NguoiDung? NguoiDangTin { get; set; }
        public ICollection<HopDong> HopDongs { get; set; }
        public ICollection<DangKyDichVu> DangKyDichVus { get; set; }
        public ICollection<AnhPhongTro> AnhPhongTros { get; set; } = new List<AnhPhongTro>();
    }
}
