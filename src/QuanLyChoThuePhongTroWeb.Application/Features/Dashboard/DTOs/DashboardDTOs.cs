using System.Collections.Generic;

namespace QuanLyChoThuePhongTroWeb.Application.Features.Dashboard.DTOs
{
    public class DashboardDataDto
    {
        // Filter criteria
        public int? SelectedBranchId { get; set; }
        public int SelectedYear { get; set; }
        public int SelectedMonth { get; set; }

        // Available items for dropdowns
        public List<BranchLookupItemDto> AvailableBranches { get; set; } = new();
        public List<int> AvailableYears { get; set; } = new();

        // KPI metrics
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

        // Chart data
        public List<string> ChartLabels { get; set; } = new();
        public List<double> ChartData { get; set; } = new();
        public List<double> ChartDataChoThu { get; set; } = new();

        // Room status data
        public List<string> RoomStatusLabels { get; set; } = new();
        public List<int> RoomStatusData { get; set; } = new();

        // To-do items & recent activities
        public List<DashboardToDoDto> ToDos { get; set; } = new();
        public List<DashboardRecentActivityDto> RecentActivities { get; set; } = new();
    }

    public class BranchLookupItemDto
    {
        public int Id { get; set; }
        public string Ten { get; set; } = string.Empty;
    }

    public class DashboardToDoDto
    {
        public string Type { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Link { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
    }

    public class DashboardRecentActivityDto
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string TimeText { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public string ColorClass { get; set; } = string.Empty;
    }

    public class DashboardInvoiceStatDto
    {
        public int Thang { get; set; }
        public QuanLyChoThuePhongTroWeb.Domain.Enums.TrangThaiHoaDon TrangThai { get; set; }
        public double TongTien { get; set; }
    }

    public class DashboardContractUtilityCheckDto
    {
        public int PhongTroId { get; set; }
        public System.DateTime ThoiDiemBatDau { get; set; }
        public int ChiNhanhId { get; set; }
        public string TenChiNhanh { get; set; } = string.Empty;
    }

    public class DashboardRecordedUtilityDto
    {
        public int PhongTroId { get; set; }
        public int Thang { get; set; }
        public int Nam { get; set; }
    }

    public class DashboardUnpaidGroupDto
    {
        public int Nam { get; set; }
        public int Thang { get; set; }
        public int ChiNhanhId { get; set; }
        public string TenChiNhanh { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    public class DashboardRecentPaymentDto
    {
        public System.DateTime NgayThanhToan { get; set; }
        public string SoPhong { get; set; } = string.Empty;
        public double SoTienThanhToan { get; set; }
        public QuanLyChoThuePhongTroWeb.Domain.Enums.PhuongThucThanhToan PhuongThuc { get; set; }
    }

    public class DashboardRecentContractDto
    {
        public System.DateTime NgayTao { get; set; }
        public string SoPhong { get; set; } = string.Empty;
        public string HoVaTen { get; set; } = string.Empty;
        public double TienThuePhong { get; set; }
    }
}
