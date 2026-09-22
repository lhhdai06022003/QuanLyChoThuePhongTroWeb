using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Domain.Entities
{
    [Table("anh_chi_so_dong_ho")]
    public class AnhChiSoDongHo
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int AnhChiSoDongHoId { get; set; }

        public int DichVuDienNuocCuaPhongId { get; set; }

        [ForeignKey(nameof(DichVuDienNuocCuaPhongId))]
        public DichVuDienNuocCuaPhong DichVuDienNuocCuaPhong { get; set; }

        public LoaiDongHo LoaiDongHo { get; set; }
        public string Url { get; set; }
        public string? PublicId { get; set; }

        public int? NguoiGuiId { get; set; }

        [ForeignKey(nameof(NguoiGuiId))]
        public NguoiDung? NguoiGui { get; set; }

        public DateTime NgayGui { get; set; } = DateTime.UtcNow;
        public TrangThaiXuLyAnhChiSo TrangThaiXuLy { get; set; } = TrangThaiXuLyAnhChiSo.MoiTaiLen;

        public decimal? GiaTriAIGoiY { get; set; }
        public double? DoTinCay { get; set; }
        public string? ThongBaoLoi { get; set; }
        public DateTime? NgayXuLy { get; set; }

        public decimal? GiaTriXacNhan { get; set; }
        public int? NguoiXacNhanId { get; set; }

        [ForeignKey(nameof(NguoiXacNhanId))]
        public NguoiDung? NguoiXacNhan { get; set; }

        public DateTime? NgayXacNhan { get; set; }
        public string? GhiChuXacNhan { get; set; }
        public bool DuocChonLamChiSoChinhThuc { get; set; }
        public bool IsDeleted { get; set; }

        public void BatDauXuLy()
        {
            if (TrangThaiXuLy != TrangThaiXuLyAnhChiSo.MoiTaiLen)
            {
                throw new InvalidOperationException("Chỉ ảnh mới tải lên mới có thể bắt đầu xử lý.");
            }

            TrangThaiXuLy = TrangThaiXuLyAnhChiSo.DangXuLy;
        }

        public void GhiNhanKetQuaAI(decimal? giaTriGoiY, double? doTinCay = null, string? thongBaoLoi = null)
        {
            if (TrangThaiXuLy != TrangThaiXuLyAnhChiSo.MoiTaiLen &&
                TrangThaiXuLy != TrangThaiXuLyAnhChiSo.DangXuLy)
            {
                throw new InvalidOperationException("Ảnh này đã có kết quả xử lý.");
            }

            if (giaTriGoiY < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(giaTriGoiY), "Chỉ số gợi ý không được âm.");
            }

            if (doTinCay is < 0 or > 1)
            {
                throw new ArgumentOutOfRangeException(nameof(doTinCay), "Độ tin cậy phải nằm trong khoảng từ 0 đến 1.");
            }

            GiaTriAIGoiY = giaTriGoiY;
            DoTinCay = doTinCay;
            ThongBaoLoi = thongBaoLoi;
            NgayXuLy = DateTime.UtcNow;
            TrangThaiXuLy = giaTriGoiY.HasValue
                ? TrangThaiXuLyAnhChiSo.DocDuoc
                : TrangThaiXuLyAnhChiSo.KhongDocDuoc;
        }

        public void GhiNhanLoiXuLy(string thongBaoLoi)
        {
            if (string.IsNullOrWhiteSpace(thongBaoLoi))
            {
                throw new ArgumentException("Thông báo lỗi không được để trống.", nameof(thongBaoLoi));
            }

            if (TrangThaiXuLy != TrangThaiXuLyAnhChiSo.MoiTaiLen &&
                TrangThaiXuLy != TrangThaiXuLyAnhChiSo.DangXuLy)
            {
                throw new InvalidOperationException("Ảnh này đã có kết quả xử lý.");
            }

            ThongBaoLoi = thongBaoLoi;
            NgayXuLy = DateTime.UtcNow;
            TrangThaiXuLy = TrangThaiXuLyAnhChiSo.Loi;
        }

        public void DanhDauCanChupLai()
        {
            if (TrangThaiXuLy != TrangThaiXuLyAnhChiSo.KhongDocDuoc &&
                TrangThaiXuLy != TrangThaiXuLyAnhChiSo.Loi)
            {
                throw new InvalidOperationException("Chỉ ảnh không đọc được hoặc xử lý lỗi mới cần chụp lại.");
            }

            TrangThaiXuLy = TrangThaiXuLyAnhChiSo.CanChupLai;
        }

        public void XacNhan(decimal giaTriXacNhan, int nguoiXacNhanId, string? ghiChu = null)
        {
            if (TrangThaiXuLy != TrangThaiXuLyAnhChiSo.DocDuoc &&
                TrangThaiXuLy != TrangThaiXuLyAnhChiSo.KhongDocDuoc)
            {
                throw new InvalidOperationException("Chỉ ảnh đã xử lý và còn hiệu lực mới có thể được xác nhận.");
            }

            if (giaTriXacNhan < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(giaTriXacNhan), "Chỉ số xác nhận không được âm.");
            }

            if (nguoiXacNhanId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(nguoiXacNhanId), "Người xác nhận không hợp lệ.");
            }

            GiaTriXacNhan = giaTriXacNhan;
            NguoiXacNhanId = nguoiXacNhanId;
            NgayXacNhan = DateTime.UtcNow;
            GhiChuXacNhan = ghiChu;
            DuocChonLamChiSoChinhThuc = true;
            TrangThaiXuLy = TrangThaiXuLyAnhChiSo.DaXacNhan;
        }

        public void DanhDauDaThayThe()
        {
            if (TrangThaiXuLy != TrangThaiXuLyAnhChiSo.CanChupLai &&
                TrangThaiXuLy != TrangThaiXuLyAnhChiSo.DaXacNhan)
            {
                throw new InvalidOperationException("Chỉ ảnh cần chụp lại hoặc đã xác nhận mới có thể được đánh dấu thay thế.");
            }

            DuocChonLamChiSoChinhThuc = false;
            TrangThaiXuLy = TrangThaiXuLyAnhChiSo.DaThayThe;
        }
    }
}
