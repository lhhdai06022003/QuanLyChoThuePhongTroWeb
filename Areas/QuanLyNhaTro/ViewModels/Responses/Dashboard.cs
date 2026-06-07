using System.Collections.Generic;
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
        public double DoanhThuThangNay { get; set; }
        public double TongTienChoThu { get; set; }
        public int SoPhongChuaThanhToan { get; set; }
        public double TyLeLapDay { get; set; }
        public int SoHopDongSapHetHan { get; set; }
        public int SoPhongChuaChotDienNuoc { get; set; }

        // --- DỮ LIỆU BIỂU ĐỒ (12 Tháng của năm) ---
        public List<string> ChartLabels { get; set; } = new List<string>();
        public List<double> ChartData { get; set; } = new List<double>();
        public List<double> ChartDataChoThu { get; set; } = new List<double>(); // Doanh thu chờ thu để làm stacked chart

        // --- DỮ LIỆU PHƯƠNG THỨC THANH TOÁN ---
        public List<string> PaymentMethodLabels { get; set; } = new List<string>();
        public List<double> PaymentMethodData { get; set; } = new List<double>();

        // --- VIỆC CẦN LÀM & HOẠT ĐỘNG GẦN ĐÂY ---
        public List<ToDoItemViewModel> ToDos { get; set; } = new List<ToDoItemViewModel>();
        public List<RecentActivityViewModel> RecentActivities { get; set; } = new List<RecentActivityViewModel>();
    }

    public class ToDoItemViewModel
    {
        public string Type { get; set; } // "danger", "warning", "info"
        public string Title { get; set; }
        public string Description { get; set; }
        public string Link { get; set; }
        public string Icon { get; set; }
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