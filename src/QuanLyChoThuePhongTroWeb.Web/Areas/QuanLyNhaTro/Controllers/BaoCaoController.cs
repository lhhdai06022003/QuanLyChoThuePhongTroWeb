using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using QuanLyChoThuePhongTroWeb.Application.Features.BaoCaos.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.BaoCaos.Services;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels.Responses;
using QuanLyChoThuePhongTroWeb.Web.Helpers;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Controllers
{
    public class BaoCaoController : AdminBaseController
    {
        private const string ExcelContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

        private readonly IBaoCaoService _service;
        private readonly IPhongTroService _phongTroService;
        private readonly TimeProvider _timeProvider;

        public BaoCaoController(IBaoCaoService service, IPhongTroService phongTroService, TimeProvider timeProvider)
        {
            _service = service;
            _phongTroService = phongTroService;
            _timeProvider = timeProvider;
        }

        [HttpGet("/QuanLyNhaTro/BaoCao")]
        public async Task<IActionResult> Index(string? tab, int? chiNhanhId, int? tuThang, int? tuNam, int? denThang, int? denNam, CancellationToken cancellationToken)
        {
            if (chiNhanhId <= 0) chiNhanhId = null;
            // Giao diện theo vai trò; chặn thật nằm ở BaoCaoService (nhân viên không xem doanh thu).
            var laAdmin = User.IsInRole("Admin");
            var tabHopLe = BaoCaoTabs.TryParse(tab, out var parsed) ? parsed : (laAdmin ? BaoCaoTab.DoanhThu : BaoCaoTab.CongNo);
            if (!laAdmin && tabHopLe == BaoCaoTab.DoanhThu)
            {
                tabHopLe = BaoCaoTab.CongNo;
            }
            var chiNhanhs = await _phongTroService.GetDanhSachChiNhanhDropdownAsync(CurrentActorId);
            var homNay = _timeProvider.GetUtcNow().UtcDateTime.AddHours(7);
            // Link "Xem giao dịch"/Bảng điều khiển có thể mang sẵn khoảng kỳ; thiếu hoặc sai thì dùng 12 kỳ mặc định.
            (int TuThang, int TuNam, int DenThang, int DenNam)? khoang = null;
            if (tuThang.HasValue && tuNam.HasValue && denThang.HasValue && denNam.HasValue
                && BaoCaoCalculator.KiemTraKhoang(tuThang.Value, tuNam.Value, denThang.Value, denNam.Value) == null)
            {
                khoang = (tuThang.Value, tuNam.Value, denThang.Value, denNam.Value);
            }

            return View(BaoCaoPageViewModel.From(BaoCaoTabs.Slug(tabHopLe), chiNhanhs, chiNhanhId, laAdmin, homNay.Month, homNay.Year, khoang));
        }

        [HttpGet("/QuanLyNhaTro/BaoCao/DuLieu")]
        public async Task<IActionResult> DuLieu(string? tab, int? chiNhanhId, int? tuThang, int? tuNam, int? denThang, int? denNam, CancellationToken cancellationToken)
        {
            if (!BaoCaoTabs.TryParse(tab, out var baoCaoTab))
            {
                return BadRequest(new { success = false, message = "Tab báo cáo không hợp lệ." });
            }

            var filter = TaoFilter(chiNhanhId, tuThang, tuNam, denThang, denNam);
            switch (baoCaoTab)
            {
                case BaoCaoTab.DoanhThu:
                {
                    var r = await _service.GetDoanhThuAsync(CurrentActorId, filter, cancellationToken);
                    return r.Success ? Ok(r.Data) : this.ToErrorResult(r);
                }
                case BaoCaoTab.CongNo:
                {
                    var r = await _service.GetCongNoAsync(CurrentActorId, filter, cancellationToken);
                    return r.Success ? Ok(r.Data) : this.ToErrorResult(r);
                }
                case BaoCaoTab.CongSuat:
                {
                    var r = await _service.GetCongSuatPhongAsync(CurrentActorId, filter, cancellationToken);
                    return r.Success ? Ok(r.Data) : this.ToErrorResult(r);
                }
                case BaoCaoTab.HopDong:
                {
                    var r = await _service.GetHopDongAsync(CurrentActorId, filter, cancellationToken);
                    return r.Success ? Ok(r.Data) : this.ToErrorResult(r);
                }
                default:
                {
                    var r = await _service.GetDienNuocAsync(CurrentActorId, filter, cancellationToken);
                    return r.Success ? Ok(r.Data) : this.ToErrorResult(r);
                }
            }
        }

        [HttpGet("/QuanLyNhaTro/BaoCao/XuatExcel")]
        public async Task<IActionResult> XuatExcel(string? tab, int? chiNhanhId, int? tuThang, int? tuNam, int? denThang, int? denNam, CancellationToken cancellationToken)
        {
            if (!BaoCaoTabs.TryParse(tab, out var baoCaoTab))
            {
                return BadRequest(new { success = false, message = "Tab báo cáo không hợp lệ." });
            }

            var filter = TaoFilter(chiNhanhId, tuThang, tuNam, denThang, denNam);
            var result = await _service.XuatExcelAsync(CurrentActorId, baoCaoTab, filter, cancellationToken);
            if (!result.Success || result.Data == null)
            {
                return this.ToErrorResult(result);
            }

            return File(result.Data.NoiDung, ExcelContentType, result.Data.TenFile);
        }

        // Thiếu tham số kỳ thì lấy mặc định: kỳ đến là tháng hiện tại (giờ VN), kỳ từ lùi 11 tháng.
        private BaoCaoFilter TaoFilter(int? chiNhanhId, int? tuThang, int? tuNam, int? denThang, int? denNam)
        {
            var homNay = _timeProvider.GetUtcNow().UtcDateTime.AddHours(7);
            var den = new DateTime(homNay.Year, homNay.Month, 1);
            var tu = den.AddMonths(-11);
            return new BaoCaoFilter
            {
                ChiNhanhId = chiNhanhId > 0 ? chiNhanhId : null,
                TuThang = tuThang ?? tu.Month,
                TuNam = tuNam ?? tu.Year,
                DenThang = denThang ?? den.Month,
                DenNam = denNam ?? den.Year
            };
        }
    }
}
