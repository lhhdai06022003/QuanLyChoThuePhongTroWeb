using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq.Expressions;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Domain.Entities
{
    // Một lượt thanh toán của khách cho một hóa đơn (trên giao diện gọi là "lượt thanh toán").
    [Table("yeu_cau_thanh_toan_hoa_don")]
    public class YeuCauThanhToanHoaDon
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int YeuCauThanhToanHoaDonId { get; set; }

        public int HoaDonId { get; set; }
        [ForeignKey("HoaDonId")]
        public HoaDon HoaDon { get; set; }

        public string MaYeuCau { get; set; }
        public decimal SoTien { get; set; }
        public string NoiDungChuyenKhoan { get; set; }
        public DateTime? HanThanhToan { get; set; }
        public TrangThaiYeuCauThanhToan TrangThai { get; set; } = TrangThaiYeuCauThanhToan.ChoThanhToan;

        public int? NguoiTaoId { get; set; }
        public DateTime NgayTao { get; set; } = DateTime.UtcNow;
        public DateTime? NgayCapNhat { get; set; }
        public bool IsDeleted { get; set; } = false;

        public ICollection<MinhChungThanhToanHoaDon> MinhChungThanhToanHoaDons { get; set; } = new List<MinhChungThanhToanHoaDon>();
        public ICollection<LichSuTrangThaiYeuCauThanhToanHoaDon> LichSuTrangThaiYeuCauThanhToanHoaDons { get; set; } = new List<LichSuTrangThaiYeuCauThanhToanHoaDon>();

        // Định nghĩa duy nhất của "lượt đang hoạt động" (spec §5.1): đang chờ đối chiếu, hoặc chờ
        // thanh toán và còn hạn. Store EF dùng trực tiếp; test fake gọi Compile().
        public static Expression<Func<YeuCauThanhToanHoaDon, bool>> DangHoatDong(DateTime nowUtc)
        {
            return y => !y.IsDeleted &&
                (y.TrangThai == TrangThaiYeuCauThanhToan.DangDoiChieu ||
                 (y.TrangThai == TrangThaiYeuCauThanhToan.ChoThanhToan && y.HanThanhToan >= nowUtc));
        }

        // Lượt chờ thanh toán đã quá hạn nhưng chưa được ghi HetHan (spec §6.3).
        public static Expression<Func<YeuCauThanhToanHoaDon, bool>> QuaHanChuaGhiNhan(DateTime nowUtc)
        {
            return y => !y.IsDeleted &&
                y.TrangThai == TrangThaiYeuCauThanhToan.ChoThanhToan &&
                y.HanThanhToan < nowUtc;
        }

        // Trạng thái để hiển thị: lượt chờ thanh toán quá hạn được coi là HetHan dù chưa ghi xuống DB.
        public static TrangThaiYeuCauThanhToan TrangThaiHieuLuc(TrangThaiYeuCauThanhToan trangThai, DateTime? hanThanhToan, DateTime nowUtc)
        {
            return trangThai == TrangThaiYeuCauThanhToan.ChoThanhToan && hanThanhToan.HasValue && hanThanhToan.Value < nowUtc
                ? TrangThaiYeuCauThanhToan.HetHan
                : trangThai;
        }

        public static YeuCauThanhToanHoaDon Tao(int hoaDonId, string maYeuCau, decimal soTien, DateTime hanThanhToanUtc, int? nguoiTaoId, DateTime nowUtc)
        {
            if (string.IsNullOrWhiteSpace(maYeuCau))
            {
                throw new ArgumentException("Mã lượt thanh toán không được để trống.", nameof(maYeuCau));
            }

            if (soTien <= 0 || soTien != decimal.Truncate(soTien))
            {
                throw new ArgumentOutOfRangeException(nameof(soTien), "Số tiền lượt thanh toán phải là số nguyên VND lớn hơn 0.");
            }

            if (hanThanhToanUtc <= nowUtc)
            {
                throw new ArgumentOutOfRangeException(nameof(hanThanhToanUtc), "Hạn lượt thanh toán phải sau thời điểm tạo.");
            }

            var yeuCau = new YeuCauThanhToanHoaDon
            {
                HoaDonId = hoaDonId,
                MaYeuCau = maYeuCau,
                SoTien = soTien,
                NoiDungChuyenKhoan = maYeuCau,
                HanThanhToan = hanThanhToanUtc,
                TrangThai = TrangThaiYeuCauThanhToan.ChoThanhToan,
                NguoiTaoId = nguoiTaoId,
                NgayTao = nowUtc
            };
            yeuCau.ThemLichSu(null, TrangThaiYeuCauThanhToan.ChoThanhToan, nguoiTaoId, nowUtc, "Tạo lượt thanh toán");
            return yeuCau;
        }

        public void DanhDauHetHan(DateTime nowUtc)
        {
            ChuyenTrangThai(TrangThaiYeuCauThanhToan.HetHan, null, nowUtc, "Quá hạn lượt thanh toán");
        }

        public void Huy(int? nguoiThucHienId, DateTime nowUtc, string lyDo)
        {
            ChuyenTrangThai(TrangThaiYeuCauThanhToan.DaHuy, nguoiThucHienId, nowUtc, lyDo);
        }

        public MinhChungThanhToanHoaDon NopMinhChung(
            string hinhAnhUrl,
            string publicId,
            string? maGiaoDichNganHang,
            DateTime ngayChuyenKhaiBaoUtc,
            int? nguoiThucHienId,
            DateTime nowUtc)
        {
            if (string.IsNullOrWhiteSpace(hinhAnhUrl))
            {
                throw new ArgumentException("Ảnh minh chứng không hợp lệ.", nameof(hinhAnhUrl));
            }

            if (TrangThai != TrangThaiYeuCauThanhToan.ChoThanhToan || !(HanThanhToan >= nowUtc))
            {
                throw new InvalidOperationException("Chỉ nộp minh chứng cho lượt đang chờ thanh toán và còn hạn.");
            }

            var minhChung = new MinhChungThanhToanHoaDon
            {
                YeuCauThanhToanHoaDonId = YeuCauThanhToanHoaDonId,
                YeuCauThanhToanHoaDon = this,
                HinhAnhUrl = hinhAnhUrl,
                PublicId = publicId,
                MaGiaoDichNganHang = maGiaoDichNganHang,
                SoTienKhaiBao = SoTien,
                NgayChuyenKhaiBao = ngayChuyenKhaiBaoUtc,
                TrangThaiDoiChieu = TrangThaiMinhChungThanhToan.ChoXacNhan,
                NgayTao = nowUtc
            };
            MinhChungThanhToanHoaDons.Add(minhChung);
            ChuyenTrangThai(TrangThaiYeuCauThanhToan.DangDoiChieu, nguoiThucHienId, nowUtc, "Khách thuê nộp minh chứng chuyển khoản");
            return minhChung;
        }

        // Từ chối minh chứng đưa lượt về chờ thanh toán với hạn mới để khách nộp lại (spec §6.1, §17).
        public void TuChoiMinhChung(MinhChungThanhToanHoaDon minhChung, int nguoiDoiChieuId, string lyDo, DateTime nowUtc, DateTime hanMoiUtc)
        {
            KiemTraMinhChungCuaLuot(minhChung);
            if (hanMoiUtc <= nowUtc)
            {
                throw new ArgumentOutOfRangeException(nameof(hanMoiUtc), "Hạn mới của lượt phải sau thời điểm từ chối.");
            }

            minhChung.TuChoi(nguoiDoiChieuId, lyDo, nowUtc);
            ChuyenTrangThai(TrangThaiYeuCauThanhToan.ChoThanhToan, nguoiDoiChieuId, nowUtc, $"Từ chối minh chứng: {lyDo}");
            HanThanhToan = hanMoiUtc;
        }

        public void HoanTat(MinhChungThanhToanHoaDon minhChung, int nguoiDoiChieuId, DateTime nowUtc, string lyDo)
        {
            KiemTraMinhChungCuaLuot(minhChung);
            minhChung.XacNhan(nguoiDoiChieuId, nowUtc);
            ChuyenTrangThai(TrangThaiYeuCauThanhToan.DaHoanTat, nguoiDoiChieuId, nowUtc, lyDo);
        }

        // Nhân viên chuyển lượt cho Admin (spec §16.1): trạng thái giữ DangDoiChieu, chỉ ghi một dòng lịch sử.
        public void GhiChuyenAdmin(int nguoiThucHienId, DateTime nowUtc, string lyDo)
        {
            if (TrangThai != TrangThaiYeuCauThanhToan.DangDoiChieu)
            {
                throw new InvalidOperationException("Chỉ chuyển Admin khi lượt đang chờ đối chiếu.");
            }

            ThemLichSu(TrangThai, TrangThai, nguoiThucHienId, nowUtc, lyDo);
            NgayCapNhat = nowUtc;
        }

        private void KiemTraMinhChungCuaLuot(MinhChungThanhToanHoaDon minhChung)
        {
            if (minhChung == null ||
                (minhChung.YeuCauThanhToanHoaDonId != YeuCauThanhToanHoaDonId && !ReferenceEquals(minhChung.YeuCauThanhToanHoaDon, this)))
            {
                throw new InvalidOperationException("Minh chứng không thuộc lượt thanh toán này.");
            }
        }

        private void ChuyenTrangThai(TrangThaiYeuCauThanhToan trangThaiMoi, int? nguoiThucHienId, DateTime nowUtc, string? lyDo)
        {
            if (!ChoPhepChuyen(TrangThai, trangThaiMoi))
            {
                throw new InvalidOperationException($"Không thể chuyển lượt thanh toán từ {TrangThai} sang {trangThaiMoi}.");
            }

            var trangThaiCu = TrangThai;
            TrangThai = trangThaiMoi;
            NgayCapNhat = nowUtc;
            ThemLichSu(trangThaiCu, trangThaiMoi, nguoiThucHienId, nowUtc, lyDo);
        }

        // Bảng chuyển trạng thái spec §6.1. DaHoanTat, DaHuy, HetHan là trạng thái cuối.
        private static bool ChoPhepChuyen(TrangThaiYeuCauThanhToan tu, TrangThaiYeuCauThanhToan den)
        {
            return tu switch
            {
                TrangThaiYeuCauThanhToan.ChoThanhToan => den is TrangThaiYeuCauThanhToan.DangDoiChieu
                    or TrangThaiYeuCauThanhToan.DaHuy
                    or TrangThaiYeuCauThanhToan.HetHan,
                TrangThaiYeuCauThanhToan.DangDoiChieu => den is TrangThaiYeuCauThanhToan.ChoThanhToan
                    or TrangThaiYeuCauThanhToan.DaHoanTat,
                _ => false
            };
        }

        private void ThemLichSu(TrangThaiYeuCauThanhToan? trangThaiCu, TrangThaiYeuCauThanhToan trangThaiMoi, int? nguoiThucHienId, DateTime nowUtc, string? lyDo)
        {
            LichSuTrangThaiYeuCauThanhToanHoaDons.Add(new LichSuTrangThaiYeuCauThanhToanHoaDon
            {
                YeuCauThanhToanHoaDonId = YeuCauThanhToanHoaDonId,
                YeuCauThanhToanHoaDon = this,
                TrangThaiCu = trangThaiCu,
                TrangThaiMoi = trangThaiMoi,
                NguoiThucHienId = nguoiThucHienId,
                NgayThucHien = nowUtc,
                LyDo = lyDo
            });
        }
    }
}
