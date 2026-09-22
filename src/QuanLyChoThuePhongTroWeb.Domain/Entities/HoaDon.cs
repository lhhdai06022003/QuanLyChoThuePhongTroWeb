using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Domain.Entities
{
    [Table("hoa_don")]
    public class HoaDon
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int HoaDonId { get; set; }
        public string MaHoaDon { get; set; }
        public int HopDongId { get; set; }
        [ForeignKey("HopDongId")]
        public HopDong HopDong { get; set; }
        public int Thang { get; set; }
        public int Nam { get; set; }
        public decimal TongTien { get; set; }
        public TrangThaiHoaDon TrangThaiHoaDon { get; set; } = TrangThaiHoaDon.ChuaThanhToan;
        public TrangThaiPhatHanhHoaDon TrangThaiPhatHanh { get; set; } = TrangThaiPhatHanhHoaDon.Nhap;

        public bool ChoPhepThanhToanMotPhan { get; set; } = false;
        public decimal? SoTienThanhToanToiThieu { get; set; }
        public DateTime? HanThanhToan { get; set; }

        public int? NguoiChotId { get; set; }
        public DateTime? NgayChot { get; set; }
        public DateTime? NgayGui { get; set; }

        public DateTime NgayTao { get; set; } = DateTime.UtcNow;
        public DateTime? NgayCapNhat { get; set; }
        public bool IsDeleted { get; set; } = false;

        // Liên kết 1-1 với điện nước (Nullable để có thể xuất hóa đơn khác)
        public int? DichVuDienNuocCuaPhongId { get; set; }
        [ForeignKey("DichVuDienNuocCuaPhongId")]
        public DichVuDienNuocCuaPhong? DichVuDienNuocCuaPhong { get; set; }

        public ICollection<ChiTietHoaDon> ChiTietHoaDonDichVus { get; set; } = new List<ChiTietHoaDon>();
        public ICollection<LichSuThanhToan> LichSuThanhToans { get; set; } = new List<LichSuThanhToan>();
        public ICollection<LichSuTrangThaiHoaDon> LichSuTrangThaiHoaDons { get; set; } = new List<LichSuTrangThaiHoaDon>();
        public ICollection<YeuCauThanhToanHoaDon> YeuCauThanhToanHoaDons { get; set; } = new List<YeuCauThanhToanHoaDon>();

        public void ChotHoaDon(int nguoiChotId, string? lyDo = null)
        {
            if (TrangThaiPhatHanh == TrangThaiPhatHanhHoaDon.DaHuy)
            {
                throw new InvalidOperationException("Không thể chốt hóa đơn đã hủy.");
            }

            var trangThaiCu = TrangThaiPhatHanh;
            TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaChot;
            NguoiChotId = nguoiChotId;
            NgayChot = DateTime.UtcNow;
            NgayCapNhat = DateTime.UtcNow;

            LichSuTrangThaiHoaDons.Add(new LichSuTrangThaiHoaDon
            {
                HoaDonId = HoaDonId,
                TrangThaiPhatHanhCu = trangThaiCu,
                TrangThaiPhatHanhMoi = TrangThaiPhatHanhHoaDon.DaChot,
                TrangThaiThanhToanCu = TrangThaiHoaDon,
                TrangThaiThanhToanMoi = TrangThaiHoaDon,
                NguoiThucHienId = nguoiChotId,
                NgayThucHien = DateTime.UtcNow,
                LyDo = lyDo
            });
        }

        public void GuiHoaDon(int? nguoiGuiId = null, string? lyDo = null)
        {
            if (TrangThaiPhatHanh != TrangThaiPhatHanhHoaDon.DaChot)
            {
                throw new InvalidOperationException("Chỉ hóa đơn đã chốt mới có thể gửi cho khách thuê.");
            }

            var trangThaiCu = TrangThaiPhatHanh;
            TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaGui;
            NgayGui = DateTime.UtcNow;
            NgayCapNhat = DateTime.UtcNow;

            LichSuTrangThaiHoaDons.Add(new LichSuTrangThaiHoaDon
            {
                HoaDonId = HoaDonId,
                TrangThaiPhatHanhCu = trangThaiCu,
                TrangThaiPhatHanhMoi = TrangThaiPhatHanhHoaDon.DaGui,
                TrangThaiThanhToanCu = TrangThaiHoaDon,
                TrangThaiThanhToanMoi = TrangThaiHoaDon,
                NguoiThucHienId = nguoiGuiId,
                NgayThucHien = DateTime.UtcNow,
                LyDo = lyDo
            });
        }

        public void HuyHoaDon(int? nguoiHuyId, string lyDo)
        {
            if (string.IsNullOrWhiteSpace(lyDo))
            {
                throw new ArgumentException("Lý do hủy hóa đơn không được để trống.", nameof(lyDo));
            }

            if (TrangThaiHoaDon == TrangThaiHoaDon.DaThanhToan)
            {
                throw new InvalidOperationException("Không thể hủy hóa đơn đã thanh toán đầy đủ.");
            }

            var trangThaiCu = TrangThaiPhatHanh;
            TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaHuy;
            NgayCapNhat = DateTime.UtcNow;

            LichSuTrangThaiHoaDons.Add(new LichSuTrangThaiHoaDon
            {
                HoaDonId = HoaDonId,
                TrangThaiPhatHanhCu = trangThaiCu,
                TrangThaiPhatHanhMoi = TrangThaiPhatHanhHoaDon.DaHuy,
                TrangThaiThanhToanCu = TrangThaiHoaDon,
                TrangThaiThanhToanMoi = TrangThaiHoaDon,
                NguoiThucHienId = nguoiHuyId,
                NgayThucHien = DateTime.UtcNow,
                LyDo = lyDo
            });
        }

        public void CauHinhThanhToanMotPhan(bool choPhep, decimal? soTienToiThieu)
        {
            if (choPhep)
            {
                if (!soTienToiThieu.HasValue || soTienToiThieu.Value <= 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(soTienToiThieu), "Số tiền thanh toán tối thiểu phải lớn hơn 0.");
                }

                if (soTienToiThieu.Value > TongTien)
                {
                    throw new ArgumentOutOfRangeException(nameof(soTienToiThieu), "Số tiền thanh toán tối thiểu không được vượt tổng tiền hóa đơn.");
                }

                ChoPhepThanhToanMotPhan = true;
                SoTienThanhToanToiThieu = soTienToiThieu.Value;
            }
            else
            {
                ChoPhepThanhToanMotPhan = false;
                SoTienThanhToanToiThieu = null;
            }

            NgayCapNhat = DateTime.UtcNow;
        }

        public bool KiemTraDuDieuKienYeuCauThanhToan()
        {
            if (TrangThaiPhatHanh != TrangThaiPhatHanhHoaDon.DaChot && TrangThaiPhatHanh != TrangThaiPhatHanhHoaDon.DaGui)
            {
                throw new InvalidOperationException("Chỉ hóa đơn đã chốt hoặc đã gửi mới được tạo yêu cầu thanh toán.");
            }

            if (TrangThaiHoaDon == TrangThaiHoaDon.DaThanhToan)
            {
                throw new InvalidOperationException("Hóa đơn đã được thanh toán hoàn tất.");
            }

            return true;
        }
    }
}
