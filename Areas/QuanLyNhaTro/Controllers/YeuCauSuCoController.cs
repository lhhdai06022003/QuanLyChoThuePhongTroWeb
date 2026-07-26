using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Logging;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Services.ChiNhanhs;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Services.YeuCauSuCos;
using QuanLyChoThuePhongTroWeb.Models;
using System;
using System.Threading.Tasks;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Controllers
{
    [Area("QuanLyNhaTro")]
    [Authorize(Roles = "Admin,NhanVien")]
    public class YeuCauSuCoController : Controller
    {
        private readonly IYeuCauSuCoService _yeuCauSuCoService;
        private readonly IChiNhanhService _chiNhanhService;
        private readonly ILogger<YeuCauSuCoController> _logger;

        public YeuCauSuCoController(IYeuCauSuCoService yeuCauSuCoService, IChiNhanhService chiNhanhService, ILogger<YeuCauSuCoController> logger)
        {
            _yeuCauSuCoService = yeuCauSuCoService;
            _chiNhanhService = chiNhanhService;
            _logger = logger;
        }

        public async Task<IActionResult> Index(int? chiNhanhId, TrangThaiSuCo? trangThai, int? soThang = 6)
        {
            var data = await _yeuCauSuCoService.GetAllAsync(chiNhanhId, trangThai, soThang);

            var chiNhanhs = await _chiNhanhService.DanhSachChiNhanh();
            ViewBag.ChiNhanhId = new SelectList(chiNhanhs, "ChiNhanhId", "TenChiNhanh", chiNhanhId);
            ViewBag.CurrentTrangThai = trangThai;
            ViewBag.CurrentSoThang = soThang ?? 6;

            return View(data);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int Id, TrangThaiSuCo TrangThai, double ChiPhiSuaChua, bool CongVaoHoaDon, string LyDoTuChoi, string GhiChuAdmin)
        {
            try
            {
                if (TrangThai == TrangThaiSuCo.DaHuy && string.IsNullOrWhiteSpace(LyDoTuChoi))
                {
                    return Json(new { success = false, message = "Vui lòng nhập lý do từ chối." });
                }

                bool success = await _yeuCauSuCoService.UpdateStatusAsync(Id, TrangThai, ChiPhiSuaChua, CongVaoHoaDon, LyDoTuChoi, GhiChuAdmin);
                
                if (success)
                    return Json(new { success = true, message = "Cập nhật trạng thái thành công." });
                else
                    return Json(new { success = false, message = "Không tìm thấy yêu cầu hoặc có lỗi xảy ra." });
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
                bool success = await _yeuCauSuCoService.SoftDeleteAsync(id);
                if (success)
                    return Json(new { success = true });
                else
                    return Json(new { success = false, message = "Lỗi khi xóa sự cố." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi xóa sự cố Id={Id}", id);
                return Json(new { success = false, message = "Lỗi hệ thống khi xóa." });
            }
        }
    }
}
