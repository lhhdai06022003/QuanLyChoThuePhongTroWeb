using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels
{
    public class DashboardViewModel
    {
        // --- TIÊU CHÍ LỌC ---
        public int? SelectedBranchId { get; set; }
        public int SelectedYear { get; set; }

        // Danh sách cho Dropdown trong View
        public IEnumerable<SelectListItem> Branches { get; set; }
        public IEnumerable<SelectListItem> Years { get; set; }

        // --- CÁC CHỈ SỐ KPI ---
        public int TongSoPhong { get; set; }
        public int SoPhongTrong { get; set; }
        public int SoPhongDaThue { get; set; }
        public int SoPhongBaoTri { get; set; }
        public int TongSoHopDongHoatDong { get; set; }
        public double DoanhThuThangNay { get; set; }
        public double TongTienChoThu { get; set; }

        // --- DỮ LIỆU BIỂU ĐỒ (12 Tháng của năm) ---
        public List<string> ChartLabels { get; set; } = new List<string>();
        public List<double> ChartData { get; set; } = new List<double>();
    }
}