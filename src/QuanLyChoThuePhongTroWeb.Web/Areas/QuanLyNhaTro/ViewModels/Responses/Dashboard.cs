using System.Collections.Generic;
using System.Globalization;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels.Responses
{
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
        public decimal DoanhThuThangNay { get; set; }
        public decimal TongTienChoThu { get; set; }
        public int SoPhongChuaThanhToan { get; set; }
        public double TyLeLapDay { get; set; }
        public string TyLeLapDayText { get; set; } = "";
        public decimal TienQuaHan { get; set; }
        public int SoHoaDonQuaHan { get; set; }

        // --- DỮ LIỆU BIỂU ĐỒ (12 Tháng của năm) ---
        public List<string> ChartLabels { get; set; } = new List<string>();
        public List<decimal> ChartData { get; set; } = new List<decimal>();
        public List<decimal> ChartDataChoThu { get; set; } = new List<decimal>(); // Doanh thu chờ thu để làm stacked chart

        // --- DỮ LIỆU TÌNH TRẠNG PHÒNG ---
        public List<string> RoomStatusLabels { get; set; } = new List<string>();
        public List<int> RoomStatusData { get; set; } = new List<int>();

        // --- VIỆC CẦN LÀM & HOẠT ĐỘNG GẦN ĐÂY ---
        public ViecCanLamKhoiViewModel ViecCanLam { get; set; } = new ViecCanLamKhoiViewModel();
        public List<RecentActivityViewModel> RecentActivities { get; set; } = new List<RecentActivityViewModel>();

        public static DashboardViewModel FromDto(QuanLyChoThuePhongTroWeb.Application.Features.Dashboard.DTOs.DashboardDataDto dto, ViecCanLamTongHopDto viecCanLam)
        {
            return new DashboardViewModel
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
                DoanhThuThangNay = dto.DoanhThuThangNay,
                TongTienChoThu = dto.TongTienChoThu,
                SoPhongChuaThanhToan = dto.SoPhongChuaThanhToan,
                TyLeLapDay = dto.TyLeLapDay,
                TyLeLapDayText = dto.TyLeLapDay.ToString("0.#", CultureInfo.InvariantCulture).Replace('.', ',') + "%",
                TienQuaHan = dto.TienQuaHan,
                SoHoaDonQuaHan = dto.SoHoaDonQuaHan,
                ChartLabels = dto.ChartLabels,
                ChartData = dto.ChartData,
                ChartDataChoThu = dto.ChartDataChoThu,
                RoomStatusLabels = dto.RoomStatusLabels,
                RoomStatusData = dto.RoomStatusData,
                ViecCanLam = ViecCanLamKhoiViewModel.FromDto(viecCanLam, 5),
                RecentActivities = dto.RecentActivities.Select(r => new RecentActivityViewModel
                {
                    Title = r.Title,
                    Description = r.Description,
                    TimeText = r.TimeText,
                    Icon = r.Icon,
                    ColorClass = r.ColorClass
                }).ToList()
            };
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