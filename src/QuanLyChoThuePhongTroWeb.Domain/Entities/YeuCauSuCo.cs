using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuanLyChoThuePhongTroWeb.Domain.Entities
{
    [Table("yeu_cau_su_co")]
    public class YeuCauSuCo
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public int PhongTroId { get; set; }
        [ForeignKey("PhongTroId")]
        public PhongTro PhongTro { get; set; }

        public int NguoiThueId { get; set; }
        [ForeignKey("NguoiThueId")]
        public NguoiThue NguoiThue { get; set; }

        [Required(ErrorMessage = "Tiêu đề không được để trống")]
        [StringLength(255)]
        public string TieuDe { get; set; }

        [Required(ErrorMessage = "Mô tả không được để trống")]
        public string MoTa { get; set; }

        public string? HinhAnhUrl { get; set; }

        public TrangThaiSuCo TrangThai { get; set; } = TrangThaiSuCo.ChoTiepNhan;

        public DateTime NgayGui { get; set; } = DateTime.UtcNow;

        public DateTime? NgayXuLy { get; set; }

        public double ChiPhiSuaChua { get; set; } = 0;

        public bool CongVaoHoaDon { get; set; } = false;

        public string? GhiChuAdmin { get; set; }
        
        public string? LyDoTuChoi { get; set; }
        
        public bool IsDeleted { get; set; } = false;
    }
}
