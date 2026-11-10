using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Features.Dashboard.DTOs;

namespace QuanLyChoThuePhongTroWeb.Application.Features.Dashboard.Services
{
    public interface IDashboardService
    {
        // Nhân viên chỉ thấy số liệu của chi nhánh được phân công; chọn chi nhánh khác thì mọi số liệu bằng 0.
        // Doanh thu (Đã thu kỳ, biểu đồ 12 tháng) chỉ trả cho Admin; nhân viên nhận null và không chạy truy vấn doanh thu.
        Task<DashboardDataDto> GetDashboardDataAsync(int actorId, int? branchId, int selectedYear, int selectedMonth);
    }
}
