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
        // Chỉ Admin: nhân viên không xem doanh thu nên các trường này là null và CoXemDoanhThu = false.
        public bool CoXemDoanhThu { get; set; }
        public decimal? DoanhThuThangNay { get; set; }
        public decimal TongTienChoThu { get; set; }
        public int SoPhongChuaThanhToan { get; set; }
        public double TyLeLapDay { get; set; }
        public decimal TienQuaHan { get; set; }
        public int SoHoaDonQuaHan { get; set; }

        // Chart data
        public List<string> ChartLabels { get; set; } = new();
        public List<decimal>? ChartData { get; set; }
        public List<decimal>? ChartDataChoThu { get; set; }

        // Room status data
        public List<string> RoomStatusLabels { get; set; } = new();
        public List<int> RoomStatusData { get; set; } = new();

        // Tiền mặt đã thu trong tháng đang chọn (giờ VN): của chính người xem; Admin có thêm bảng theo từng người thu.
        public DashboardTienMatDto TienMatCuaToi { get; set; } = new();
        // Chỉ Admin; nhân viên là null.
        public List<DashboardTienMatNguoiDto>? TienMatNhanVien { get; set; }

        // Recent activities
        public List<DashboardRecentActivityDto> RecentActivities { get; set; } = new();
    }

    public class DashboardTienMatDto
    {
        public int SoKhoan { get; set; }
        public decimal TongTien { get; set; }
    }

    public class DashboardTienMatNguoiDto
    {
        public int NguoiDungId { get; set; }
        public string Ten { get; set; } = string.Empty;
        public int SoKhoan { get; set; }
        public decimal TongTien { get; set; }
    }

    public class BranchLookupItemDto
    {
        public int Id { get; set; }
        public string Ten { get; set; } = string.Empty;
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
        // Tổng sổ chưa xóa của hóa đơn đã gửi, chưa xóa trong tháng.
        public decimal DaThu { get; set; }
        // Tổng còn nợ của hóa đơn đã gửi khách, chưa thanh toán đủ trong tháng.
        public decimal ChoThu { get; set; }
    }

    public class DashboardCongNoDto
    {
        // Tổng còn nợ của hóa đơn đã gửi khách, chưa thu đủ, thuộc kỳ đang chọn.
        public decimal ChoThuThang { get; set; }
        // Số phòng có hóa đơn đã gửi chưa thu đủ (mọi kỳ).
        public int SoPhongDangNo { get; set; }
        public int SoHoaDonQuaHan { get; set; }
        public decimal TienQuaHan { get; set; }
    }

    public class DashboardRecentPaymentDto
    {
        public System.DateTime NgayThanhToan { get; set; }
        public string SoPhong { get; set; } = string.Empty;
        public decimal SoTienThanhToan { get; set; }
        public QuanLyChoThuePhongTroWeb.Domain.Enums.PhuongThucThanhToan PhuongThuc { get; set; }
        public string? MaGiaoDich { get; set; }
    }

    public class DashboardRecentContractDto
    {
        public System.DateTime NgayTao { get; set; }
        public string SoPhong { get; set; } = string.Empty;
        public string HoVaTen { get; set; } = string.Empty;
        public decimal TienThuePhong { get; set; }
    }
}
