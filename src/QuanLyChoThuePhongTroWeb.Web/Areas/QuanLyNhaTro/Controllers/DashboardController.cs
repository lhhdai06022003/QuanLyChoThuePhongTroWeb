using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels.Responses;
using QuanLyChoThuePhongTroWeb.Web.Helpers;

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
            var (selectedYear, selectedMonth) = ChuanHoaKy(year, month);

            var dto = await _dashboardService.GetDashboardDataAsync(CurrentActorId, branchId, selectedYear, selectedMonth);
            var viec = await _viecCanLamService.GetTongHopAsync(CurrentActorId, branchId, cancellationToken);
            var model = DashboardViewModel.FromDto(dto, viec);
            if (branchId == null)
            {
                // Cùng phạm vi với huy hiệu (TomTat tính với branchId null): layout in sẵn để huy hiệu không gọi lại.
                ViewData[ViecCanLamBadge.ViewDataKey] = viec;
            }
            return View(model);
        }

        // Tháng ngoài 1..12 hoặc năm ngoài 2000..2100 (hoặc thiếu) thì dùng tháng/năm hiện tại theo giờ Việt Nam.
        private (int Year, int Month) ChuanHoaKy(int? year, int? month)
        {
            var nowVn = _timeProvider.GetUtcNow().UtcDateTime.AddHours(7);
            var y = year is >= 2000 and <= 2100 ? year.Value : nowVn.Year;
            var m = month is >= 1 and <= 12 ? month.Value : nowVn.Month;
            return (y, m);
        }

        [HttpGet("/QuanLyNhaTro/Dashboard/GetAjaxData")]
        public async Task<IActionResult> GetAjaxData(int? branchId, int year, int month, CancellationToken cancellationToken)
        {
            var (selectedYear, selectedMonth) = ChuanHoaKy(year, month);
            var model = await _dashboardService.GetDashboardDataAsync(CurrentActorId, branchId, selectedYear, selectedMonth);
            var viecDayDu = await _viecCanLamService.GetTongHopAsync(CurrentActorId, branchId, cancellationToken);
            // Admin: việc HD2 nằm ở khối "Chờ bạn duyệt" riêng, không nằm trong "Việc cần làm".
            var (viec, _) = DashboardViecCanLam.TachChoDuyet(viecDayDu, model.CoXemDoanhThu);
            var vm = DashboardViewModel.FromDto(model, viecDayDu);

            return Json(new
            {
                tongSoPhong = model.TongSoPhong,
                soPhongTrong = model.SoPhongTrong,
                soPhongDaThue = model.SoPhongDaThue,
                soPhongBaoTri = model.SoPhongBaoTri,
                tongSoHopDongHoatDong = model.TongSoHopDongHoatDong,
                coXemDoanhThu = model.CoXemDoanhThu,
                doanhThuThangNay = model.DoanhThuThangNay,
                tongTienChoThu = model.TongTienChoThu,
                soPhongChuaThanhToan = model.SoPhongChuaThanhToan,
                tyLeLapDay = model.TyLeLapDay,
                tienQuaHan = model.TienQuaHan,
                soHoaDonQuaHan = model.SoHoaDonQuaHan,
                soHopDongSapHetHan = viec.Items.Where(i => i.MaViec == "KH3").Sum(i => i.SoLuong),
                soPhongChuaChotDienNuoc = viec.Items.Where(i => i.MaViec == "CS1").Sum(i => i.SoLuong),

                tienMatCuaToi = new { soKhoan = vm.TienMatCuaToi.SoKhoan, tongTien = vm.TienMatCuaToi.TongTien },
                tienMatNhanVien = vm.TienMatNhanVien?.Select(n => new
                {
                    nguoiDungId = n.NguoiDungId,
                    ten = n.Ten,
                    soKhoan = n.SoKhoan,
                    tongTien = n.TongTien,
                    link = n.Link
                }),
                choDuyet = vm.ChoDuyet == null ? null : new { soLuong = vm.ChoDuyet.SoLuong, tieuDe = vm.ChoDuyet.TieuDe, link = vm.ChoDuyet.Link },
                links = new
                {
                    quaHan = vm.LinkQuaHan,
                    choThu = vm.LinkChoThu,
                    phongDangNo = vm.LinkPhongDangNo,
                    tienMatCuaToi = vm.LinkTienMatCuaToi,
                    daThuKy = vm.LinkDaThuKy,
                    baoCaoNam = vm.LinkBaoCaoNam
                },
                
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
