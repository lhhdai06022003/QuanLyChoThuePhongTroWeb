using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels.Responses;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Controllers
{
    public class DashboardController : AdminBaseController
    {
        private readonly IDashboardService _dashboardService;
        private readonly IViecCanLamService _viecCanLamService;
        private readonly TimeProvider _timeProvider;

        public DashboardController(IDashboardService dashboardService, IViecCanLamService viecCanLamService, TimeProvider timeProvider)
        {
            _dashboardService = dashboardService;
            _viecCanLamService = viecCanLamService;
            _timeProvider = timeProvider;
        }

        [Route("/QuanLyNhaTro/Dashboard")]
        [Route("/QuanLyNhaTro/Dashboard/Index")]
        public async Task<IActionResult> Index(int? branchId, int? year, int? month, CancellationToken cancellationToken)
        {
            var nowVn = _timeProvider.GetUtcNow().UtcDateTime.AddHours(7);
            int selectedYear = year ?? nowVn.Year;
            int selectedMonth = month ?? nowVn.Month;

            var dto = await _dashboardService.GetDashboardDataAsync(CurrentActorId, branchId, selectedYear, selectedMonth);
            var viec = await _viecCanLamService.GetTongHopAsync(CurrentActorId, branchId, cancellationToken);
            var model = DashboardViewModel.FromDto(dto, viec);
            return View(model);
        }

        [HttpGet("/QuanLyNhaTro/Dashboard/GetAjaxData")]
        public async Task<IActionResult> GetAjaxData(int? branchId, int year, int month, CancellationToken cancellationToken)
        {
            var model = await _dashboardService.GetDashboardDataAsync(CurrentActorId, branchId, year, month);
            var viec = await _viecCanLamService.GetTongHopAsync(CurrentActorId, branchId, cancellationToken);

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
                tienQuaHan = model.TienQuaHan,
                soHoaDonQuaHan = model.SoHoaDonQuaHan,
                soHopDongSapHetHan = viec.Items.Where(i => i.MaViec == "KH3").Sum(i => i.SoLuong),
                soPhongChuaChotDienNuoc = viec.Items.Where(i => i.MaViec == "CS1").Sum(i => i.SoLuong),
                
                chartLabels = model.ChartLabels,
                chartData = model.ChartData,
                chartDataChoThu = model.ChartDataChoThu,
                
                roomStatusLabels = model.RoomStatusLabels,
                roomStatusData = model.RoomStatusData,
                
                viecCanLam = new
                {
                    tongSo = viec.TongSo,
                    soKhan = viec.SoKhan,
                    soCanLam = viec.SoCanLam,
                    soTheoDoi = viec.SoTheoDoi,
                    items = viec.Items.Take(5).Select(i => new
                    {
                        maViec = i.MaViec,
                        nhom = i.Nhom.ToString(),
                        mucUuTien = i.MucUuTien.ToString(),
                        tieuDe = i.TieuDe,
                        moTa = i.MoTa,
                        soLuong = i.SoLuong,
                        link = i.Link
                    })
                },
                recentActivities = model.RecentActivities
            });
        }
    }
}
