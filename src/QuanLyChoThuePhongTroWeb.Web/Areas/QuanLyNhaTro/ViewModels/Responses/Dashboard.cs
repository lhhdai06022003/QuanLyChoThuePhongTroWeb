using System.Collections.Generic;
using System.Globalization;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels.Responses
{
    // Đường dẫn từ ô số của Bảng điều khiển sang màn chi tiết; một nguồn cho Razor và JSON.
    // chiNhanhId null thì bỏ hẳn tham số. Tháng/năm là kỳ đang chọn trên Bảng điều khiển.
    public static class DashboardLinks
    {
        private static string ChiNhanh(int? chiNhanhId, string noi) =>
            chiNhanhId.HasValue ? noi + "chiNhanhId=" + chiNhanhId.Value.ToString(CultureInfo.InvariantCulture) : "";

        public static string QuaHan(int? chiNhanhId) =>
            "/QuanLyNhaTro/ViecCanLam" + ChiNhanh(chiNhanhId, "?") + "#hoa-don-qua-han";

        public static string ChoThu(int? chiNhanhId, int thang, int nam) =>
            $"/QuanLyNhaTro/QuanLyHoaDon?thang={thang}&nam={nam}&trangThaiPhatHanh=3&trangThai=-2" + ChiNhanh(chiNhanhId, "&");

        public static string PhongDangNo(int? chiNhanhId) =>
            "/QuanLyNhaTro/QuanLyHoaDon?thang=0&nam=0&trangThaiPhatHanh=3&trangThai=-2" + ChiNhanh(chiNhanhId, "&");

        // nguoiThu: "toi" hoặc NguoiDungId.
        public static string TienMat(string nguoiThu, int? chiNhanhId, int thang, int nam)
        {
            var dau = new System.DateTime(nam, thang, 1);
            var cuoi = dau.AddMonths(1).AddDays(-1);
            return $"/QuanLyNhaTro/LichSuThanhToan?nguoiThu={nguoiThu}&phuongThuc=tien-mat"
                + $"&tuNgay={dau.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}&denNgay={cuoi.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}"
                + ChiNhanh(chiNhanhId, "&");
        }

        public static string DaThuKy(int? chiNhanhId, int thang, int nam) =>
            "/QuanLyNhaTro/BaoCao?tab=doanh-thu" + ChiNhanh(chiNhanhId, "&") + $"&tuThang={thang}&tuNam={nam}&denThang={thang}&denNam={nam}";

        // Nút "Xem báo cáo chi tiết" của biểu đồ 12 tháng: cả năm đang xem, giữ chi nhánh.
        public static string BaoCaoNam(int? chiNhanhId, int nam) =>
            "/QuanLyNhaTro/BaoCao?tab=doanh-thu" + ChiNhanh(chiNhanhId, "&") + $"&tuThang=1&tuNam={nam}&denThang=12&denNam={nam}";

        public static string ChoDuyetKhongCoViec(int? chiNhanhId) =>
            "/QuanLyNhaTro/QuanLyHoaDon?thang=0&nam=0&trangThaiPhatHanh=1" + ChiNhanh(chiNhanhId, "&");
    }

    // Việc HD2 (hóa đơn chờ Admin chốt) có khối riêng "Chờ bạn duyệt" nên được tách khỏi "Việc cần làm" của Admin.
    public static class DashboardViecCanLam
    {
        public static (ViecCanLamTongHopDto ConLai, ViecCanLamDto? Hd2) TachChoDuyet(ViecCanLamTongHopDto viec, bool laAdmin)
        {
            if (!laAdmin)
            {
                return (viec, null);
            }

            ViecCanLamDto? hd2 = null;
            var conLai = new List<ViecCanLamDto>();
            foreach (var item in viec.Items)
            {
                if (item.MaViec == "HD2")
                {
                    hd2 = item;
                }
                else
                {
                    conLai.Add(item);
                }
            }

            if (hd2 == null)
            {
                return (viec, null);
            }

            return (new ViecCanLamTongHopDto
            {
                TongSo = viec.TongSo - hd2.SoLuong,
                SoKhan = viec.SoKhan - (hd2.MucUuTien == MucUuTienViec.Khan ? hd2.SoLuong : 0),
                SoCanLam = viec.SoCanLam - (hd2.MucUuTien == MucUuTienViec.CanLam ? hd2.SoLuong : 0),
                SoTheoDoi = viec.SoTheoDoi - (hd2.MucUuTien == MucUuTienViec.TheoDoi ? hd2.SoLuong : 0),
                Items = conLai
            }, hd2);
        }
    }

    public class DashboardTienMatViewModel
    {
        public int SoKhoan { get; set; }
        public decimal TongTien { get; set; }
    }

    public class DashboardTienMatNguoiViewModel
    {
        public int NguoiDungId { get; set; }
        public string Ten { get; set; } = "";
        public int SoKhoan { get; set; }
        public decimal TongTien { get; set; }
        public string Link { get; set; } = "";
    }

    public class DashboardChoDuyetViewModel
    {
        public int SoLuong { get; set; }
        public string TieuDe { get; set; } = "Hóa đơn chờ Admin chốt";
        public string Link { get; set; } = "";
    }

    public class DashboardViewModel
    {
        // --- TIÊU CHÍ LỌC ---
        public int? SelectedBranchId { get; set; }
        public int SelectedYear { get; set; }
        public int SelectedMonth { get; set; }

        // Danh sách cho Dropdown trong View
        public IEnumerable<SelectListItem> Branches { get; set; }
        public IEnumerable<SelectListItem> Years { get; set; }
        public IEnumerable<SelectListItem> Months { get; set; }

        // --- CÁC CHỈ SỐ KPI ---
        public int TongSoPhong { get; set; }
        public int SoPhongTrong { get; set; }
        public int SoPhongDaThue { get; set; }
        public int SoPhongBaoTri { get; set; }
        public int TongSoHopDongHoatDong { get; set; }
        // Doanh thu chỉ dành cho Admin: nhân viên có CoXemDoanhThu = false và các trường này là null.
        public bool CoXemDoanhThu { get; set; }
        public decimal? DoanhThuThangNay { get; set; }
        public decimal TongTienChoThu { get; set; }
        public int SoPhongChuaThanhToan { get; set; }
        public double TyLeLapDay { get; set; }
        public string TyLeLapDayText { get; set; } = "";
        public decimal TienQuaHan { get; set; }
        public int SoHoaDonQuaHan { get; set; }

        // --- TIỀN MẶT ĐÃ THU TRONG THÁNG ---
        public DashboardTienMatViewModel TienMatCuaToi { get; set; } = new DashboardTienMatViewModel();
        // Chỉ Admin (null với nhân viên).
        public List<DashboardTienMatNguoiViewModel>? TienMatNhanVien { get; set; }
        public int TienMatNhanVienTongKhoan { get; set; }
        public decimal TienMatNhanVienTongTien { get; set; }

        // Chỉ Admin (null với nhân viên): khối "Chờ bạn duyệt".
        public DashboardChoDuyetViewModel? ChoDuyet { get; set; }

        // --- ĐƯỜNG DẪN CỦA CÁC Ô SỐ ---
        public string LinkQuaHan { get; set; } = "";
        public string LinkChoThu { get; set; } = "";
        public string LinkPhongDangNo { get; set; } = "";
        public string LinkTienMatCuaToi { get; set; } = "";
        public string? LinkDaThuKy { get; set; }
        public string? LinkBaoCaoNam { get; set; }

        // --- DỮ LIỆU BIỂU ĐỒ (12 Tháng của năm) ---
        public List<string> ChartLabels { get; set; } = new List<string>();
        public List<decimal>? ChartData { get; set; }
        public List<decimal>? ChartDataChoThu { get; set; } // Doanh thu chờ thu để làm stacked chart

        // --- DỮ LIỆU TÌNH TRẠNG PHÒNG ---
        public List<string> RoomStatusLabels { get; set; } = new List<string>();
        public List<int> RoomStatusData { get; set; } = new List<int>();

        // --- VIỆC CẦN LÀM & HOẠT ĐỘNG GẦN ĐÂY ---
        public ViecCanLamKhoiViewModel ViecCanLam { get; set; } = new ViecCanLamKhoiViewModel();
        public List<RecentActivityViewModel> RecentActivities { get; set; } = new List<RecentActivityViewModel>();

        public static DashboardViewModel FromDto(QuanLyChoThuePhongTroWeb.Application.Features.Dashboard.DTOs.DashboardDataDto dto, ViecCanLamTongHopDto viecCanLam)
        {
            var laAdmin = dto.CoXemDoanhThu;
            var (conLai, hd2) = DashboardViecCanLam.TachChoDuyet(viecCanLam, laAdmin);
            int? cn = dto.SelectedBranchId;

            var model = new DashboardViewModel
            {
                SelectedBranchId = dto.SelectedBranchId,
                SelectedYear = dto.SelectedYear,
                SelectedMonth = dto.SelectedMonth,
                Branches = new SelectList(dto.AvailableBranches, "Id", "Ten", dto.SelectedBranchId),
                Years = new SelectList(dto.AvailableYears, dto.SelectedYear),
                Months = System.Linq.Enumerable.Range(1, 12).Select(m => new SelectListItem
                {
                    Value = m.ToString(),
                    Text = $"Tháng {m}",
                    Selected = (m == dto.SelectedMonth)
                }).ToList(),
                TongSoPhong = dto.TongSoPhong,
                SoPhongTrong = dto.SoPhongTrong,
                SoPhongDaThue = dto.SoPhongDaThue,
                SoPhongBaoTri = dto.SoPhongBaoTri,
                TongSoHopDongHoatDong = dto.TongSoHopDongHoatDong,
                CoXemDoanhThu = dto.CoXemDoanhThu,
                DoanhThuThangNay = dto.DoanhThuThangNay,
                TongTienChoThu = dto.TongTienChoThu,
                SoPhongChuaThanhToan = dto.SoPhongChuaThanhToan,
                TyLeLapDay = dto.TyLeLapDay,
                TyLeLapDayText = dto.TyLeLapDay.ToString("0.#", CultureInfo.InvariantCulture).Replace('.', ',') + "%",
                TienQuaHan = dto.TienQuaHan,
                SoHoaDonQuaHan = dto.SoHoaDonQuaHan,
                TienMatCuaToi = new DashboardTienMatViewModel { SoKhoan = dto.TienMatCuaToi.SoKhoan, TongTien = dto.TienMatCuaToi.TongTien },
                LinkQuaHan = DashboardLinks.QuaHan(cn),
                LinkChoThu = DashboardLinks.ChoThu(cn, dto.SelectedMonth, dto.SelectedYear),
                LinkPhongDangNo = DashboardLinks.PhongDangNo(cn),
                LinkTienMatCuaToi = DashboardLinks.TienMat("toi", cn, dto.SelectedMonth, dto.SelectedYear),
                LinkDaThuKy = laAdmin ? DashboardLinks.DaThuKy(cn, dto.SelectedMonth, dto.SelectedYear) : null,
                LinkBaoCaoNam = laAdmin ? DashboardLinks.BaoCaoNam(cn, dto.SelectedYear) : null,
                ChartLabels = dto.ChartLabels,
                ChartData = dto.ChartData,
                ChartDataChoThu = dto.ChartDataChoThu,
                RoomStatusLabels = dto.RoomStatusLabels,
                RoomStatusData = dto.RoomStatusData,
                ViecCanLam = ViecCanLamKhoiViewModel.FromDto(conLai, 5),
                RecentActivities = dto.RecentActivities.Select(r => new RecentActivityViewModel
                {
                    Title = r.Title,
                    Description = r.Description,
                    TimeText = r.TimeText,
                    Icon = r.Icon,
                    ColorClass = r.ColorClass
                }).ToList()
            };

            if (dto.TienMatNhanVien != null)
            {
                model.TienMatNhanVien = dto.TienMatNhanVien.Select(n => new DashboardTienMatNguoiViewModel
                {
                    NguoiDungId = n.NguoiDungId,
                    Ten = n.Ten,
                    SoKhoan = n.SoKhoan,
                    TongTien = n.TongTien,
                    Link = DashboardLinks.TienMat(n.NguoiDungId.ToString(CultureInfo.InvariantCulture), cn, dto.SelectedMonth, dto.SelectedYear)
                }).ToList();
                model.TienMatNhanVienTongKhoan = model.TienMatNhanVien.Sum(n => n.SoKhoan);
                model.TienMatNhanVienTongTien = model.TienMatNhanVien.Sum(n => n.TongTien);
            }

            if (laAdmin)
            {
                model.ChoDuyet = new DashboardChoDuyetViewModel
                {
                    SoLuong = hd2?.SoLuong ?? 0,
                    TieuDe = hd2?.TieuDe ?? "Hóa đơn chờ Admin chốt",
                    Link = hd2?.Link ?? DashboardLinks.ChoDuyetKhongCoViec(cn)
                };
            }

            return model;
        }
    }

    public class RecentActivityViewModel
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public string TimeText { get; set; }
        public string Icon { get; set; }
        public string ColorClass { get; set; } // "bg-success", "bg-primary", etc.
    }
}
