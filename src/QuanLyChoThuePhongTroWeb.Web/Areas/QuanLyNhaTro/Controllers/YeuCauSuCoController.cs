using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Logging;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Security;
using QuanLyChoThuePhongTroWeb.Models;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Controllers
{
    [Area("QuanLyNhaTro")]
    [Authorize(Roles = "Admin,NhanVien")]
    public class YeuCauSuCoController : Controller
    {
        private readonly IYeuCauSuCoService _yeuCauSuCoService;
        private readonly IChiNhanhService _chiNhanhService;
        private readonly IThongBaoService _thongBaoService;
        private readonly ILogger<YeuCauSuCoController> _logger;
        private readonly IEmployeeAccessService _employeeAccessService;

        public YeuCauSuCoController(
            IYeuCauSuCoService yeuCauSuCoService,
            IChiNhanhService chiNhanhService,
            IThongBaoService thongBaoService,
            ILogger<YeuCauSuCoController> logger,
            IEmployeeAccessService employeeAccessService)
        {
            _yeuCauSuCoService = yeuCauSuCoService;
            _chiNhanhService = chiNhanhService;
            _thongBaoService = thongBaoService;
            _logger = logger;
            _employeeAccessService = employeeAccessService;
        }

        public async Task<IActionResult> Index(int? chiNhanhId, QuanLyChoThuePhongTroWeb.Application.Common.Enums.AppTrangThaiSuCo? trangThai, int? soThang = 6)
        {
            var data = await _yeuCauSuCoService.GetAllAsync(chiNhanhId, trangThai, soThang);

            var chiNhanhs = await _chiNhanhService.DanhSachChiNhanh();
            ViewBag.ChiNhanhId = new SelectList(chiNhanhs, "ChiNhanhId", "TenChiNhanh", chiNhanhId);
            ViewBag.CurrentTrangThai = trangThai;
            ViewBag.CurrentSoThang = soThang ?? 6;

            var chuaPhanCong = false;
            if (User.IsInRole("NhanVien") && int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId))
            {
                var scope = await _employeeAccessService.GetScopeAsync(actorId);
                chuaPhanCong = scope != null && !scope.IsAdmin && scope.ActiveBranchIds.Count == 0;
            }
            ViewBag.ChuaPhanCongChiNhanh = chuaPhanCong;

            return View(data);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int Id, QuanLyChoThuePhongTroWeb.Application.Common.Enums.AppTrangThaiSuCo TrangThai, decimal ChiPhiSuaChua, bool CongVaoHoaDon, string LyDoTuChoi, string GhiChuAdmin)
        {
            try
            {
                if (TrangThai == QuanLyChoThuePhongTroWeb.Application.Common.Enums.AppTrangThaiSuCo.DaHuy && string.IsNullOrWhiteSpace(LyDoTuChoi))
                {
                    return Json(new { success = false, message = "Vui lòng nhập lý do từ chối." });
                }

                var actorIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!int.TryParse(actorIdStr, out var actorId) || actorId <= 0)
                {
                    return Json(new { success = false, message = "Người dùng chưa đăng nhập hoặc phiên làm việc đã hết hạn." });
                }

                var suco = await _yeuCauSuCoService.GetByIdAsync(Id);
                if (suco == null) return Json(new { success = false, message = "Không tìm thấy sự cố." });

                var result = await _yeuCauSuCoService.UpdateStatusAsync(Id, TrangThai, ChiPhiSuaChua, CongVaoHoaDon, LyDoTuChoi, GhiChuAdmin, actorId);
                
                if (result.Success)
                {
                    string statusName = TrangThai switch
                    {
                        QuanLyChoThuePhongTroWeb.Application.Common.Enums.AppTrangThaiSuCo.ChoTiepNhan => "Chờ tiếp nhận",
                        QuanLyChoThuePhongTroWeb.Application.Common.Enums.AppTrangThaiSuCo.DangXuLy => "Đang xử lý",
                        QuanLyChoThuePhongTroWeb.Application.Common.Enums.AppTrangThaiSuCo.DaHoanThanh => "Đã hoàn thành",
                        QuanLyChoThuePhongTroWeb.Application.Common.Enums.AppTrangThaiSuCo.DaHuy => "Đã hủy/Từ chối",
                        _ => TrangThai.ToString()
                    };
                    
                    string msg = $"Sự cố '{suco.TieuDe}' của bạn đã chuyển sang trạng thái: '{statusName}'";
                    await _thongBaoService.GuiChoKhachThueAsync(suco.NguoiThueId, "Cập nhật sự cố", msg, "SuCo", "/KhachThue/SuCo");
                    
                    return Json(new { success = true, message = "Cập nhật trạng thái thành công." });
                }
                else
                {
                    return Json(new { success = false, message = result.Message });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi cập nhật sự cố Id={Id}", Id);
                return Json(new { success = false, message = "Lỗi hệ thống khi cập nhật." });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var actorIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!int.TryParse(actorIdStr, out var actorId) || actorId <= 0)
                {
                    return Json(new { success = false, message = "Người dùng chưa đăng nhập hoặc phiên làm việc đã hết hạn." });
                }

                var result = await _yeuCauSuCoService.SoftDeleteAsync(id, actorId);
                return Json(new { success = result.Success, message = result.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi xóa sự cố Id={Id}", id);
                return Json(new { success = false, message = "Lỗi hệ thống khi xóa." });
            }
        }
    }
}
