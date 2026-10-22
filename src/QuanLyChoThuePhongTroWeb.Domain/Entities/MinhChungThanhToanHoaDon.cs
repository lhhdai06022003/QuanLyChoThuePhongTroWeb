using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Domain.Entities
{
    [Table("minh_chung_thanh_toan_hoa_don")]
    public class MinhChungThanhToanHoaDon
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int MinhChungThanhToanHoaDonId { get; set; }

        public int YeuCauThanhToanHoaDonId { get; set; }
        [ForeignKey("YeuCauThanhToanHoaDonId")]
        public YeuCauThanhToanHoaDon YeuCauThanhToanHoaDon { get; set; }

        public string HinhAnhUrl { get; set; }
        public string? PublicId { get; set; }
        public string? MaGiaoDichNganHang { get; set; }
        public decimal SoTienKhaiBao { get; set; }
        public DateTime NgayChuyenKhaiBao { get; set; }
        public TrangThaiMinhChungThanhToan TrangThaiDoiChieu { get; set; } = TrangThaiMinhChungThanhToan.ChoXacNhan;

        public int? NguoiDoiChieuId { get; set; }
        public DateTime? NgayDoiChieu { get; set; }
        public string? LyDoTuChoi { get; set; }

        public DateTime NgayTao { get; set; } = DateTime.UtcNow;
        public bool IsDeleted { get; set; } = false;

        public LichSuThanhToan? LichSuThanhToan { get; set; }

        public void XacNhan(int nguoiDoiChieuId, DateTime nowUtc)
        {
            KiemTraChoXacNhan();
            TrangThaiDoiChieu = TrangThaiMinhChungThanhToan.DaXacNhan;
            NguoiDoiChieuId = nguoiDoiChieuId;
            NgayDoiChieu = nowUtc;
        }

        public void TuChoi(int nguoiDoiChieuId, string lyDo, DateTime nowUtc)
        {
            if (string.IsNullOrWhiteSpace(lyDo))
            {
                throw new ArgumentException("Lý do từ chối minh chứng không được để trống.", nameof(lyDo));
            }

            KiemTraChoXacNhan();
            TrangThaiDoiChieu = TrangThaiMinhChungThanhToan.TuChoi;
            LyDoTuChoi = lyDo;
            NguoiDoiChieuId = nguoiDoiChieuId;
            NgayDoiChieu = nowUtc;
        }

        private void KiemTraChoXacNhan()
        {
            if (TrangThaiDoiChieu != TrangThaiMinhChungThanhToan.ChoXacNhan)
            {
                throw new InvalidOperationException("Minh chứng đã được xử lý trước đó.");
            }
        }
    }
}
