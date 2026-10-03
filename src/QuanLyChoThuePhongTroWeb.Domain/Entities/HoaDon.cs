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

        public void KhoiTaoNhap(int nguoiTaoId)
        {
            if (nguoiTaoId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(nguoiTaoId), "Người tạo hóa đơn không hợp lệ.");
            }

            if (TrangThaiPhatHanh != TrangThaiPhatHanhHoaDon.Nhap)
            {
                throw new InvalidOperationException($"Chỉ hóa đơn ở trạng thái {TrangThaiPhatHanhHoaDon.Nhap} mới có thể khởi tạo nháp.");
            }

            if (LichSuTrangThaiHoaDons.Count > 0)
            {
                throw new InvalidOperationException("Hóa đơn đã được khởi tạo lịch sử trước đó.");
            }

            LichSuTrangThaiHoaDons.Add(new LichSuTrangThaiHoaDon
            {
                HoaDonId = HoaDonId,
                TrangThaiPhatHanhCu = null,
                TrangThaiPhatHanhMoi = TrangThaiPhatHanhHoaDon.Nhap,
                TrangThaiThanhToanCu = TrangThaiHoaDon,
                TrangThaiThanhToanMoi = TrangThaiHoaDon,
                NguoiThucHienId = nguoiTaoId,
                NgayThucHien = DateTime.UtcNow,
                LyDo = "Tạo hóa đơn nháp"
            });
        }

        public void GuiDuyet(int? nguoiThucHienId = null, string? lyDo = null)
        {
            if (TrangThaiPhatHanh == TrangThaiPhatHanhHoaDon.DaHuy)
            {
                throw new InvalidOperationException("Không thể gửi duyệt hóa đơn đã hủy.");
            }

            if (TrangThaiPhatHanh != TrangThaiPhatHanhHoaDon.Nhap)
            {
                throw new InvalidOperationException($"Chỉ hóa đơn ở trạng thái {TrangThaiPhatHanhHoaDon.Nhap} mới có thể gửi duyệt.");
            }

            var trangThaiCu = TrangThaiPhatHanh;
            TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.ChoDuyet;
            NgayCapNhat = DateTime.UtcNow;

            LichSuTrangThaiHoaDons.Add(new LichSuTrangThaiHoaDon
            {
                HoaDonId = HoaDonId,
                TrangThaiPhatHanhCu = trangThaiCu,
                TrangThaiPhatHanhMoi = TrangThaiPhatHanhHoaDon.ChoDuyet,
                TrangThaiThanhToanCu = TrangThaiHoaDon,
                TrangThaiThanhToanMoi = TrangThaiHoaDon,
                NguoiThucHienId = nguoiThucHienId,
                NgayThucHien = DateTime.UtcNow,
                LyDo = lyDo
            });
        }

        public void TraLai(int? nguoiThucHienId, string lyDo)
        {
            if (string.IsNullOrWhiteSpace(lyDo))
            {
                throw new ArgumentException("Lý do trả lại hóa đơn không được để trống.", nameof(lyDo));
            }

            if (TrangThaiPhatHanh == TrangThaiPhatHanhHoaDon.DaHuy)
            {
                throw new InvalidOperationException("Không thể trả lại hóa đơn đã hủy.");
            }

            if (TrangThaiPhatHanh != TrangThaiPhatHanhHoaDon.ChoDuyet)
            {
                throw new InvalidOperationException($"Chỉ có thể trả lại hóa đơn khi đang ở trạng thái {TrangThaiPhatHanhHoaDon.ChoDuyet}.");
            }

            var trangThaiCu = TrangThaiPhatHanh;
            TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.Nhap;
            NgayCapNhat = DateTime.UtcNow;

            LichSuTrangThaiHoaDons.Add(new LichSuTrangThaiHoaDon
            {
                HoaDonId = HoaDonId,
                TrangThaiPhatHanhCu = trangThaiCu,
                TrangThaiPhatHanhMoi = TrangThaiPhatHanhHoaDon.Nhap,
                TrangThaiThanhToanCu = TrangThaiHoaDon,
                TrangThaiThanhToanMoi = TrangThaiHoaDon,
                NguoiThucHienId = nguoiThucHienId,
                NgayThucHien = DateTime.UtcNow,
                LyDo = lyDo
            });
        }

        public void ChotHoaDon(int nguoiChotId, string? lyDo = null)
        {
            if (TrangThaiPhatHanh == TrangThaiPhatHanhHoaDon.DaHuy)
            {
                throw new InvalidOperationException("Không thể chốt hóa đơn đã hủy.");
            }

            if (TrangThaiPhatHanh != TrangThaiPhatHanhHoaDon.ChoDuyet)
            {
                throw new InvalidOperationException("Chỉ hóa đơn ở trạng thái chờ duyệt mới có thể chốt.");
            }

            if (nguoiChotId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(nguoiChotId), "Người chốt không hợp lệ.");
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

        public void GuiHoaDon(int nguoiGuiId, DateTime nowUtc, DateTime hanThanhToanMacDinhUtc, string? lyDo = null)
        {
            if (nguoiGuiId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(nguoiGuiId), "Người gửi hóa đơn không hợp lệ.");
            }

            if (nowUtc.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentOutOfRangeException(nameof(nowUtc), "Thời điểm gửi hóa đơn phải là UTC.");
            }

            if (hanThanhToanMacDinhUtc <= nowUtc)
            {
                throw new ArgumentOutOfRangeException(nameof(hanThanhToanMacDinhUtc), "Hạn thanh toán mặc định phải sau thời điểm gửi.");
            }

            if (TrangThaiPhatHanh == TrangThaiPhatHanhHoaDon.DaHuy)
            {
                throw new InvalidOperationException("Không thể gửi hóa đơn đã hủy.");
            }

            if (TrangThaiPhatHanh != TrangThaiPhatHanhHoaDon.DaChot)
            {
                throw new InvalidOperationException("Chỉ hóa đơn đã chốt mới có thể gửi cho khách thuê.");
            }

            var trangThaiCu = TrangThaiPhatHanh;
            TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaGui;
            NgayGui = nowUtc;
            NgayCapNhat = nowUtc;
            HanThanhToan ??= hanThanhToanMacDinhUtc;

            LichSuTrangThaiHoaDons.Add(new LichSuTrangThaiHoaDon
            {
                HoaDonId = HoaDonId,
                TrangThaiPhatHanhCu = trangThaiCu,
                TrangThaiPhatHanhMoi = TrangThaiPhatHanhHoaDon.DaGui,
                TrangThaiThanhToanCu = TrangThaiHoaDon,
                TrangThaiThanhToanMoi = TrangThaiHoaDon,
                NguoiThucHienId = nguoiGuiId,
                NgayThucHien = nowUtc,
                LyDo = lyDo
            });
        }

        public void HuyHoaDon(int? nguoiHuyId, string lyDo)
        {
            if (string.IsNullOrWhiteSpace(lyDo))
            {
                throw new ArgumentException("Lý do hủy hóa đơn không được để trống.", nameof(lyDo));
            }

            if (TrangThaiPhatHanh == TrangThaiPhatHanhHoaDon.DaHuy)
            {
                throw new InvalidOperationException("Hóa đơn đã bị hủy trước đó.");
            }

            if (TrangThaiPhatHanh != TrangThaiPhatHanhHoaDon.DaChot && TrangThaiPhatHanh != TrangThaiPhatHanhHoaDon.DaGui)
            {
                throw new InvalidOperationException("Chỉ hóa đơn đã chốt hoặc đã gửi mới có thể hủy.");
            }

            if (TrangThaiHoaDon != TrangThaiHoaDon.ChuaThanhToan)
            {
                throw new InvalidOperationException("Không thể hủy hóa đơn đã thanh toán đầy đủ hoặc thanh toán một phần.");
            }

            var trangThaiCu = TrangThaiPhatHanh;
            TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaHuy;
            IsDeleted = true;
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

        // nguoiThucHienId có giá trị thì ghi một dòng lịch sử (trạng thái giữ nguyên) làm dấu vết cấu hình.
        public void CauHinhThanhToanMotPhan(bool choPhep, decimal? soTienToiThieu, int? nguoiThucHienId = null, DateTime? nowUtc = null)
        {
            if (choPhep)
            {
                if (!soTienToiThieu.HasValue || soTienToiThieu.Value <= 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(soTienToiThieu), "Số tiền thanh toán tối thiểu phải lớn hơn 0.");
                }

                if (soTienToiThieu.Value != decimal.Truncate(soTienToiThieu.Value))
                {
                    throw new ArgumentOutOfRangeException(nameof(soTienToiThieu), "Số tiền thanh toán tối thiểu phải là số nguyên VND.");
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

            var now = nowUtc ?? DateTime.UtcNow;
            NgayCapNhat = now;

            if (nguoiThucHienId.HasValue)
            {
                ThemLichSuThanhToan(TrangThaiHoaDon, nguoiThucHienId, now, choPhep
                    ? $"Bật thanh toán một phần, tối thiểu {SoTienThanhToanToiThieu:#,##0}"
                    : "Tắt thanh toán một phần");
            }
        }

        // Method thay vì property để EF không coi là cột.
        public bool TongTienLaSoNguyen() => TongTien == decimal.Truncate(TongTien);

        // Tính lại trạng thái thanh toán từ tổng các khoản ghi nhận chưa xóa mềm (không cộng dồn).
        // Trả về true nếu trạng thái đổi. Tổng âm hoặc vượt tổng tiền là dữ liệu hỏng nên ném lỗi.
        public bool CapNhatTrangThaiThanhToan(decimal tongDaThanhToan, int? nguoiThucHienId, string? lyDo, DateTime nowUtc, bool luonGhiLichSu = false)
        {
            if (tongDaThanhToan < 0)
            {
                throw new InvalidOperationException("Tổng đã thanh toán của hóa đơn không được âm.");
            }

            if (tongDaThanhToan > TongTien)
            {
                throw new InvalidOperationException($"Tổng đã thanh toán ({tongDaThanhToan:0.##}) vượt tổng tiền hóa đơn ({TongTien:0.##}).");
            }

            var trangThaiMoi = tongDaThanhToan == 0
                ? TrangThaiHoaDon.ChuaThanhToan
                : tongDaThanhToan == TongTien ? TrangThaiHoaDon.DaThanhToan : TrangThaiHoaDon.ThanhToanMotPhan;

            var daDoi = trangThaiMoi != TrangThaiHoaDon;
            if (!daDoi && !luonGhiLichSu)
            {
                return false;
            }

            ThemLichSuThanhToan(trangThaiMoi, nguoiThucHienId, nowUtc, lyDo);
            TrangThaiHoaDon = trangThaiMoi;
            NgayCapNhat = nowUtc;
            return daDoi;
        }

        private void ThemLichSuThanhToan(TrangThaiHoaDon trangThaiMoi, int? nguoiThucHienId, DateTime nowUtc, string? lyDo)
        {
            LichSuTrangThaiHoaDons.Add(new LichSuTrangThaiHoaDon
            {
                HoaDonId = HoaDonId,
                TrangThaiPhatHanhCu = TrangThaiPhatHanh,
                TrangThaiPhatHanhMoi = TrangThaiPhatHanh,
                TrangThaiThanhToanCu = TrangThaiHoaDon,
                TrangThaiThanhToanMoi = trangThaiMoi,
                NguoiThucHienId = nguoiThucHienId,
                NgayThucHien = nowUtc,
                LyDo = lyDo
            });
        }

        public bool KiemTraDuDieuKienYeuCauThanhToan()
        {
            if (TrangThaiPhatHanh != TrangThaiPhatHanhHoaDon.DaGui || IsDeleted)
            {
                throw new InvalidOperationException("Chỉ hóa đơn đã gửi cho khách thuê mới được tạo yêu cầu thanh toán.");
            }

            if (TrangThaiHoaDon == TrangThaiHoaDon.DaThanhToan)
            {
                throw new InvalidOperationException("Hóa đơn đã được thanh toán hoàn tất.");
            }

            return true;
        }
    }
}
