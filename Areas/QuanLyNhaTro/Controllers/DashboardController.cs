using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Services.Dashboard;
using System;
using System.Threading.Tasks;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Controllers
{
    [Area("QuanLyNhaTro")]
    [Authorize(Roles = "Admin,NhanVien")]
    [AutoValidateAntiforgeryToken]
    public class DashboardController : Controller
    {
        private readonly IDashboardService _dashboardService;

        public DashboardController(IDashboardService dashboardService)
        {
            _dashboardService = dashboardService;
        }

        [Route("/QuanLyNhaTro/Dashboard")]
        [Route("/QuanLyNhaTro/Dashboard/Index")]
        public async Task<IActionResult> Index(int? branchId, int? year, int? month)
        {
            var today = DateTime.Now;
            int selectedYear = year ?? today.Year;
            int selectedMonth = month ?? today.Month;

            var model = await _dashboardService.GetDashboardDataAsync(branchId, selectedYear, selectedMonth);
            return View(model);
        }

        [HttpGet("/QuanLyNhaTro/Dashboard/GetAjaxData")]
        public async Task<IActionResult> GetAjaxData(int? branchId, int year, int month)
        {
            var model = await _dashboardService.GetDashboardDataAsync(branchId, year, month);
            
            return Json(new
            {
                tongSoPhong = model.TongSoPhong,
                soPhongTrong = model.SoPhongTrong,
                soPhongDaThue = model.SoPhongDaThue,
                soPhongBaoTri = model.SoPhongBaoTri,
                tongSoHopDongHoatDong = model.TongSoHopDongHoatDong,
                doanhThuThangNay = model.DoanhThuThangNay,
                tongTienChoThu = model.TongTienChoThu,
                soPhongChuaThanhToan = model.SoPhongChuaThanhToan,
                tyLeLapDay = model.TyLeLapDay,
                soHopDongSapHetHan = model.SoHopDongSapHetHan,
                soPhongChuaChotDienNuoc = model.SoPhongChuaChotDienNuoc,
                
                chartLabels = model.ChartLabels,
                chartData = model.ChartData,
                chartDataChoThu = model.ChartDataChoThu,
                
                roomStatusLabels = model.RoomStatusLabels,
                roomStatusData = model.RoomStatusData,
                
                toDos = model.ToDos,
                recentActivities = model.RecentActivities
            });
        }
    }
}
