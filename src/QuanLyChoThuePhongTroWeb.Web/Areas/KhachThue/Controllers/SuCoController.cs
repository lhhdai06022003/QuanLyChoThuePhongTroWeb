using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Logging;
using QuanLyChoThuePhongTroWeb.Models;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace QuanLyChoThuePhongTroWeb.Areas.KhachThue.Controllers
{
    [Area("KhachThue")]
    [Authorize]
    public class SuCoController : Controller
    {
        private readonly IYeuCauSuCoService _yeuCauSuCoService;
        private readonly IHopDongService _hopDongService;
        private readonly IImageStorageService _imageStorageService;
        private readonly IThongBaoService _thongBaoService;
        private readonly ILogger<SuCoController> _logger;

        private const long MaxFileSize = 5 * 1024 * 1024; // 5MB
        private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".webp" };

        public SuCoController(
            IYeuCauSuCoService yeuCauSuCoService,
            IHopDongService hopDongService,
            IImageStorageService imageStorageService,
            IThongBaoService thongBaoService,
            ILogger<SuCoController> logger)
        {
            _yeuCauSuCoService = yeuCauSuCoService;
            _hopDongService = hopDongService;
            _imageStorageService = imageStorageService;
            _thongBaoService = thongBaoService;
            _logger = logger;
        }

        private int GetNguoiThueId()
        {
            var claim = User.FindFirst("NguoiThueId");
            return claim != null ? int.Parse(claim.Value) : 0;
        }

        public async Task<IActionResult> Index()
        {
            int nguoiThueId = GetNguoiThueId();
            if (nguoiThueId == 0) return Content("Lỗi xác thực NguoiThueId.");

            var hopDongs = await _hopDongService.GetHopDongsByNguoiThueIdAsync(nguoiThueId);
            var phongTros = hopDongs.Select(h => new { Id = h.PhongTroId, TenPhong = h.SoPhong }).ToList();
            ViewBag.PhongTroId = new SelectList(phongTros, "Id", "TenPhong");

            var danhSachSuCo = await _yeuCauSuCoService.GetByNguoiThueAsync(nguoiThueId);
            return View(danhSachSuCo);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(QuanLyChoThuePhongTroWeb.Application.Features.YeuCauSuCos.DTOs.CreateYeuCauSuCoReq model, System.Collections.Generic.List<IFormFile> HinhAnhFiles)
        {
            int nguoiThueId = GetNguoiThueId();
            model.NguoiThueId = nguoiThueId;
            model.TrangThai = QuanLyChoThuePhongTroWeb.Application.Common.Enums.AppTrangThaiSuCo.ChoTiepNhan;

            try
            {
                var urlList = new System.Collections.Generic.List<string>();

                if (HinhAnhFiles != null && HinhAnhFiles.Count > 0)
                {
                    foreach (var file in HinhAnhFiles)
                    {
                        if (file.Length > MaxFileSize)
                        {
                            return Json(new { success = false, message = $"Kích thước file {file.FileName} vượt quá 5MB." });
                        }

                        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
                        if (!AllowedExtensions.Contains(ext))
                        {
                            return Json(new { success = false, message = $"Chỉ chấp nhận file ảnh (.jpg, .png, .gif, .webp). Lỗi ở file {file.FileName}." });
                        }
                    }

                    // Nếu pass qua hết validation thì bắt đầu upload
                    foreach (var file in HinhAnhFiles)
                    {
                        using var stream = file.OpenReadStream();
                        var uploadFile = new QuanLyChoThuePhongTroWeb.Application.Common.Files.UploadFile(
                            stream,
                            file.FileName,
                            file.ContentType,
                            file.Length);
                        var url = await _imageStorageService.UploadImageAsync(uploadFile, "suco");
                        if (!string.IsNullOrEmpty(url))
                        {
                            urlList.Add(url);
                        }
                    }
                }

                model.HinhAnhUrl = urlList.Count > 0 ? string.Join(",", urlList) : null;

                ModelState.Remove("PhongTro");
                ModelState.Remove("NguoiThue");
                ModelState.Remove("HinhAnhUrl");
                ModelState.Remove("HinhAnhFiles"); // Just in case it's in model state

                if (ModelState.IsValid)
                {
                    bool success = await _yeuCauSuCoService.CreateAsync(model);
                    if (success)
                    {
                        var hopDongs = await _hopDongService.GetHopDongsByNguoiThueIdAsync(nguoiThueId);
                        var phong = hopDongs.FirstOrDefault(h => h.PhongTroId == model.PhongTroId);
                        string tenPhong = phong != null ? phong.SoPhong : "chưa rõ";

                        string msg = $"Phòng {tenPhong} vừa báo cáo sự cố: '{model.TieuDe}'";
                        await _thongBaoService.GuiChoQuyenAsync(QuanLyChoThuePhongTroWeb.Application.Common.Enums.AppRole.Admin, "Sự cố mới", msg, "SuCo", "/QuanLyNhaTro/YeuCauSuCo");
                        await _thongBaoService.GuiChoQuyenAsync(QuanLyChoThuePhongTroWeb.Application.Common.Enums.AppRole.NhanVien, "Sự cố mới", msg, "SuCo", "/QuanLyNhaTro/YeuCauSuCo");

                        return Json(new { success = true, message = "Gửi báo cáo sự cố thành công!" });
                    }
                }
                
                var errors = string.Join("<br/>", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                return Json(new { success = false, message = "Dữ liệu không hợp lệ: <br/>" + errors });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi tạo báo cáo sự cố cho NguoiThueId={NguoiThueId}", nguoiThueId);
                return Json(new { success = false, message = "Đã xảy ra lỗi hệ thống khi gửi báo cáo." });
            }
        }
    }
}
