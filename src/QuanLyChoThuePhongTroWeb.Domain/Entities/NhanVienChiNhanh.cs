using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuanLyChoThuePhongTroWeb.Domain.Entities
{
    [Table("nhan_vien_chi_nhanh")]
    public class NhanVienChiNhanh
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int NhanVienChiNhanhId { get; set; }

        public int NguoiDungId { get; set; }
        [ForeignKey("NguoiDungId")]
        public NguoiDung NguoiDung { get; set; }

        public int ChiNhanhId { get; set; }
        [ForeignKey("ChiNhanhId")]
        public ChiNhanh ChiNhanh { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime NgayPhanCong { get; set; } = DateTime.UtcNow;

        public int NguoiPhanCongId { get; set; }
        [ForeignKey("NguoiPhanCongId")]
        public NguoiDung NguoiPhanCong { get; set; }

        public DateTime? NgayThuHoi { get; set; }

        public int? NguoiThuHoiId { get; set; }
        [ForeignKey("NguoiThuHoiId")]
        public NguoiDung? NguoiThuHoi { get; set; }

        [MaxLength(500)]
        public string? LyDo { get; set; }

        public void ThuHoi(int actorId, DateTime nowUtc, string lyDo)
        {
            if (string.IsNullOrWhiteSpace(lyDo))
            {
                throw new ArgumentException("Lý do thu hồi không được để trống.", nameof(lyDo));
            }

            if (lyDo.Trim().Length > 500)
            {
                throw new ArgumentException("Lý do thu hồi tối đa 500 ký tự.", nameof(lyDo));
            }

            if (!DangHieuLuc())
            {
                throw new InvalidOperationException("Phân công không đang hiệu lực nên không thể thu hồi.");
            }

            IsActive = false;
            NgayThuHoi = nowUtc;
            NguoiThuHoiId = actorId;
            LyDo = lyDo.Trim();
        }

        public void PhanCongLai(int actorId, DateTime nowUtc, string? lyDo)
        {
            var normalized = string.IsNullOrWhiteSpace(lyDo) ? null : lyDo.Trim();

            if (normalized?.Length > 500)
            {
                throw new ArgumentException("Lý do tối đa 500 ký tự.", nameof(lyDo));
            }

            if (DangHieuLuc())
            {
                throw new InvalidOperationException("Phân công đang hiệu lực nên không thể phân công lại.");
            }

            IsActive = true;
            NgayPhanCong = nowUtc;
            NguoiPhanCongId = actorId;
            NgayThuHoi = null;
            NguoiThuHoiId = null;
            LyDo = normalized;
        }

        private bool DangHieuLuc() => IsActive && NgayThuHoi == null;
    }
}
