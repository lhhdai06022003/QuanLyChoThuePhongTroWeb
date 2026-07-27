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
        private readonly QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Services.ThongBaos.IThongBaoService _thongBaoService;
        private readonly ILogger<YeuCauSuCoController> _logger;

        public YeuCauSuCoController(
            IYeuCauSuCoService yeuCauSuCoService, 
            IChiNhanhService chiNhanhService, 
            QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Services.ThongBaos.IThongBaoService thongBaoService,
            ILogger<YeuCauSuCoController> logger)
        {
            _yeuCauSuCoService = yeuCauSuCoService;
            _chiNhanhService = chiNhanhService;
            _thongBaoService = thongBaoService;
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

                var suco = await _yeuCauSuCoService.GetByIdAsync(Id);
                if (suco == null) return Json(new { success = false, message = "Không tìm thấy sự cố." });

                bool success = await _yeuCauSuCoService.UpdateStatusAsync(Id, TrangThai, ChiPhiSuaChua, CongVaoHoaDon, LyDoTuChoi, GhiChuAdmin);
                
                if (success)
                {
                    string statusName = TrangThai switch
                    {
                        TrangThaiSuCo.ChoTiepNhan => "Chờ tiếp nhận",
                        TrangThaiSuCo.DangXuLy => "Đang xử lý",
                        TrangThaiSuCo.DaHoanThanh => "Đã hoàn thành",
                        TrangThaiSuCo.DaHuy => "Đã hủy/Từ chối",
                        _ => TrangThai.ToString()
                    };
                    
                    string msg = $"Sự cố '{suco.TieuDe}' của bạn đã chuyển sang trạng thái: '{statusName}'";
                    await _thongBaoService.GuiChoKhachThueAsync(suco.NguoiThueId, "Cập nhật sự cố", msg, "SuCo", "/KhachThue/SuCo");
                    
                    return Json(new { success = true, message = "Cập nhật trạng thái thành công." });
                }
                else
                {
                    return Json(new { success = false, message = "Không tìm thấy yêu cầu hoặc có lỗi xảy ra." });
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
