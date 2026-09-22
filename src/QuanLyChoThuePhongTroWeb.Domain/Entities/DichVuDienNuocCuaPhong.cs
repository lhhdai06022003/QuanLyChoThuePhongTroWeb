using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Domain.Entities
{
    [Table("dich_vu_dien_nuoc_cua_phong")]
    public class DichVuDienNuocCuaPhong
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int DichVuDienNuocCuaPhongId { get; set; }
        public int PhongTroId { get; set; }
        [ForeignKey("PhongTroId")]
        public PhongTro PhongTro { get; set; }
        public int Thang { get; set; }
        public int Nam { get; set; }
        public decimal ChiSoDienCu { get; set; }
        public decimal ChiSoDienMoi { get; set; }
        public decimal DonGiaDien { get; set; }

        public decimal ChiSoNuocCu { get; set; }
        public decimal ChiSoNuocMoi { get; set; }
        public decimal DonGiaNuoc { get; set; }

        public TrangThaiGhiNhan TrangThaiGhiNhan { get; set; } = TrangThaiGhiNhan.Nhap;
        public int? NguoiTaoId { get; set; }
        public int? NguoiDuyetId { get; set; }
        public DateTime? NgayDuyet { get; set; }
        public string? GhiChuDuyet { get; set; }

        public DateTime NgayTao { get; set; } = DateTime.UtcNow;
        public DateTime? NgayCapNhat { get; set; }
        public bool IsDeleted { get; set; } = false;

        public ICollection<HoaDon> HoaDons { get; set; } = new List<HoaDon>();
        public ICollection<AnhChiSoDongHo> AnhChiSoDongHos { get; set; } = new List<AnhChiSoDongHo>();

        public void GuiDuyet()
        {
            if (TrangThaiGhiNhan != TrangThaiGhiNhan.Nhap && TrangThaiGhiNhan != TrangThaiGhiNhan.TuChoi)
            {
                throw new InvalidOperationException($"Không thể gửi duyệt từ trạng thái {TrangThaiGhiNhan}.");
            }

            TrangThaiGhiNhan = TrangThaiGhiNhan.ChoDuyet;
            NgayCapNhat = DateTime.UtcNow;
        }

        public void Duyet(int nguoiDuyetId, string? ghiChu = null)
        {
            if (TrangThaiGhiNhan != TrangThaiGhiNhan.ChoDuyet)
            {
                throw new InvalidOperationException($"Chỉ có thể duyệt khi ở trạng thái {TrangThaiGhiNhan.ChoDuyet}.");
            }

            TrangThaiGhiNhan = TrangThaiGhiNhan.DaDuyet;
            NguoiDuyetId = nguoiDuyetId;
            NgayDuyet = DateTime.UtcNow;
            GhiChuDuyet = ghiChu;
            NgayCapNhat = DateTime.UtcNow;
        }

        public void TuChoi(int nguoiDuyetId, string lyDo)
        {
            if (string.IsNullOrWhiteSpace(lyDo))
            {
                throw new ArgumentException("Lý do từ chối không được để trống.", nameof(lyDo));
            }

            if (TrangThaiGhiNhan != TrangThaiGhiNhan.ChoDuyet)
            {
                throw new InvalidOperationException($"Chỉ có thể từ chối khi ở trạng thái {TrangThaiGhiNhan.ChoDuyet}.");
            }

            TrangThaiGhiNhan = TrangThaiGhiNhan.TuChoi;
            NguoiDuyetId = nguoiDuyetId;
            NgayDuyet = DateTime.UtcNow;
            GhiChuDuyet = lyDo;
            NgayCapNhat = DateTime.UtcNow;
        }

        public void CapNhatChiSo(decimal chiSoDienMoi, decimal chiSoNuocMoi)
        {
            if (chiSoDienMoi < ChiSoDienCu)
            {
                throw new InvalidOperationException("Chỉ số điện mới không được nhỏ hơn chỉ số điện cũ.");
            }

            if (chiSoNuocMoi < ChiSoNuocCu)
            {
                throw new InvalidOperationException("Chỉ số nước mới không được nhỏ hơn chỉ số nước cũ.");
            }

            ChiSoDienMoi = chiSoDienMoi;
            ChiSoNuocMoi = chiSoNuocMoi;
            NgayCapNhat = DateTime.UtcNow;
        }
    }
}
