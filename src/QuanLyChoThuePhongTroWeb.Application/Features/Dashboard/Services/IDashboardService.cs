using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Features.Dashboard.DTOs;

namespace QuanLyChoThuePhongTroWeb.Application.Features.Dashboard.Services
{
    public interface IDashboardService
    {
        Task<DashboardDataDto> GetDashboardDataAsync(int? branchId, int selectedYear, int selectedMonth);
    }
}
