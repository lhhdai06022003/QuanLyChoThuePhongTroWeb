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

        public const int MaxThongBaoLoiLength = 1000;

        public static DateTime TruncateToMicroseconds(DateTime t)
        {
            return new DateTime(t.Ticks - (t.Ticks % 10), t.Kind == DateTimeKind.Unspecified ? DateTimeKind.Utc : t.Kind);
        }

        private void CapNhatNgayXuLy(DateTime utcNow)
        {
            var candidate = TruncateToMicroseconds(utcNow);
            if (NgayXuLy.HasValue && candidate <= NgayXuLy.Value)
            {
                candidate = NgayXuLy.Value.AddTicks(10);
            }
            NgayXuLy = candidate;
        }

        public void BatDauXuLy()
        {
            BatDauXuLy(DateTime.UtcNow);
        }

        public void BatDauXuLy(DateTime utcNow)
        {
            if (TrangThaiXuLy != TrangThaiXuLyAnhChiSo.MoiTaiLen)
            {
                throw new InvalidOperationException("Chỉ ảnh mới tải lên mới có thể bắt đầu xử lý.");
            }

            TrangThaiXuLy = TrangThaiXuLyAnhChiSo.DangXuLy;
            CapNhatNgayXuLy(utcNow);
        }

        public void ThuLaiXuLy()
        {
            ThuLaiXuLy(DateTime.UtcNow, TimeSpan.FromMinutes(10));
        }

        public void ThuLaiXuLy(DateTime utcNow, TimeSpan quaHanSau)
        {
            if (DuocChonLamChiSoChinhThuc ||
                TrangThaiXuLy == TrangThaiXuLyAnhChiSo.DaXacNhan ||
                TrangThaiXuLy == TrangThaiXuLyAnhChiSo.DaThayThe ||
                IsDeleted)
            {
                throw new InvalidOperationException("Ảnh đã xác nhận, đã thay thế hoặc đã bị xóa không thể thử xử lý lại.");
            }

            if (TrangThaiXuLy == TrangThaiXuLyAnhChiSo.MoiTaiLen || TrangThaiXuLy == TrangThaiXuLyAnhChiSo.DangXuLy)
            {
                var thoiDiemGoc = NgayXuLy ?? NgayGui;
                if (thoiDiemGoc > utcNow - quaHanSau)
                {
                    throw new InvalidOperationException("Ảnh đang được xử lý chưa quá hạn, không thể thử xử lý lại.");
                }
            }
            else if (TrangThaiXuLy != TrangThaiXuLyAnhChiSo.KhongDocDuoc &&
                     TrangThaiXuLy != TrangThaiXuLyAnhChiSo.Loi)
            {
                throw new InvalidOperationException("Chỉ ảnh không đọc được, xử lý lỗi hoặc bị kẹt quá hạn mới có thể thử xử lý lại.");
            }

            TrangThaiXuLy = TrangThaiXuLyAnhChiSo.DangXuLy;
            ThongBaoLoi = null;
            CapNhatNgayXuLy(utcNow);
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

            if (thongBaoLoi != null && thongBaoLoi.Length > MaxThongBaoLoiLength)
            {
                thongBaoLoi = thongBaoLoi[..MaxThongBaoLoiLength];
            }

            GiaTriAIGoiY = giaTriGoiY;
            DoTinCay = doTinCay;
            ThongBaoLoi = thongBaoLoi;
            CapNhatNgayXuLy(DateTime.UtcNow);
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

            if (thongBaoLoi.Length > MaxThongBaoLoiLength)
            {
                thongBaoLoi = thongBaoLoi[..MaxThongBaoLoiLength];
            }

            ThongBaoLoi = thongBaoLoi;
            CapNhatNgayXuLy(DateTime.UtcNow);
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
            if (TrangThaiXuLy == TrangThaiXuLyAnhChiSo.MoiTaiLen || TrangThaiXuLy == TrangThaiXuLyAnhChiSo.DangXuLy)
            {
                throw new InvalidOperationException("Ảnh chưa được xử lý AI xong, không thể xác nhận.");
            }

            if (TrangThaiXuLy == TrangThaiXuLyAnhChiSo.KhongDocDuoc)
            {
                if (string.IsNullOrWhiteSpace(ghiChu))
                {
                    throw new InvalidOperationException("Ảnh không đọc được bắt buộc phải có lý do khi xác nhận.");
                }
            }
            else if (TrangThaiXuLy != TrangThaiXuLyAnhChiSo.DocDuoc)
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

        public void XacNhanThuCong(decimal giaTriXacNhan, int nguoiXacNhanId, string lyDo)
        {
            if (string.IsNullOrWhiteSpace(lyDo))
            {
                throw new ArgumentException("Lý do xác nhận thủ công không được để trống.", nameof(lyDo));
            }

            if (TrangThaiXuLy == TrangThaiXuLyAnhChiSo.MoiTaiLen || TrangThaiXuLy == TrangThaiXuLyAnhChiSo.DangXuLy)
            {
                throw new InvalidOperationException("Ảnh chưa được xử lý AI xong, không thể xác nhận thủ công.");
            }

            if (TrangThaiXuLy != TrangThaiXuLyAnhChiSo.DocDuoc &&
                TrangThaiXuLy != TrangThaiXuLyAnhChiSo.KhongDocDuoc &&
                TrangThaiXuLy != TrangThaiXuLyAnhChiSo.Loi)
            {
                throw new InvalidOperationException("Chỉ ảnh còn hiệu lực mới có thể được xác nhận thủ công.");
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
            GhiChuXacNhan = lyDo;
            DuocChonLamChiSoChinhThuc = true;
            TrangThaiXuLy = TrangThaiXuLyAnhChiSo.DaXacNhan;
        }

        public void HuyChonChinhThuc()
        {
            DuocChonLamChiSoChinhThuc = false;
            if (TrangThaiXuLy == TrangThaiXuLyAnhChiSo.DaXacNhan || TrangThaiXuLy == TrangThaiXuLyAnhChiSo.CanChupLai)
            {
                TrangThaiXuLy = TrangThaiXuLyAnhChiSo.DaThayThe;
            }
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

        public void SuaGiaTriXacNhan(decimal giaTriMoi, int nguoiSuaId, string lyDo)
        {
            if (IsDeleted)
            {
                throw new InvalidOperationException("Ảnh đã bị xóa, không thể sửa số.");
            }

            if (TrangThaiXuLy != TrangThaiXuLyAnhChiSo.DaXacNhan)
            {
                throw new InvalidOperationException("Chỉ ảnh đã xác nhận mới có thể sửa số.");
            }

            if (!DuocChonLamChiSoChinhThuc)
            {
                throw new InvalidOperationException("Chỉ ảnh đang là chỉ số chính thức mới có thể sửa số.");
            }

            if (string.IsNullOrWhiteSpace(lyDo))
            {
                throw new ArgumentException("Lý do sửa số không được để trống.", nameof(lyDo));
            }

            if (giaTriMoi < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(giaTriMoi), "Chỉ số xác nhận không được âm.");
            }

            if (nguoiSuaId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(nguoiSuaId), "Người sửa không hợp lệ.");
            }

            if (GiaTriXacNhan.HasValue && giaTriMoi == GiaTriXacNhan.Value)
            {
                throw new InvalidOperationException("Số mới trùng với số đã xác nhận.");
            }

            decimal giaTriCu = GiaTriXacNhan ?? 0m;
            GiaTriXacNhan = giaTriMoi;
            NguoiXacNhanId = nguoiSuaId;
            NgayXacNhan = DateTime.UtcNow;

            var ghiChu = $"[Sửa {giaTriCu} → {giaTriMoi}] {lyDo.Trim()}";
            if (ghiChu.Length > 1000)
            {
                ghiChu = ghiChu[..1000];
            }
            GhiChuXacNhan = ghiChu;

            TrangThaiXuLy = TrangThaiXuLyAnhChiSo.DaXacNhan;
            DuocChonLamChiSoChinhThuc = true;
        }
    }
}
