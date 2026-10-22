using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Features.Dashboard.DTOs;

namespace QuanLyChoThuePhongTroWeb.Application.Features.Dashboard.Services
{
    public interface IDashboardService
    {
        // Nhân viên chỉ thấy số liệu của chi nhánh được phân công; chọn chi nhánh khác thì mọi số liệu bằng 0.
        Task<DashboardDataDto> GetDashboardDataAsync(int actorId, int? branchId, int selectedYear, int selectedMonth);
    }
}
