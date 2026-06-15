using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels.Responses;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Services.Dashboard
{
    public interface IDashboardService
    {
        Task<DashboardViewModel> GetDashboardDataAsync(int? branchId, int selectedYear, int selectedMonth);
    }
}
