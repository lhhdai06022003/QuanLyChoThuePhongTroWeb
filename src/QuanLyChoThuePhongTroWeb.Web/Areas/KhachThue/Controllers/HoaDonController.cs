using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Services;

namespace QuanLyChoThuePhongTroWeb.Areas.KhachThue.Controllers
{
    [Area("KhachThue")]
    [Authorize(Roles = "KhachThue")]
    public class HoaDonController : Controller
    {
        private readonly IHoaDonService _hoaDonService;
        private readonly IInvoiceViewService _invoiceViewService;

        public HoaDonController(
            IHoaDonService hoaDonService,
            IInvoiceViewService invoiceViewService)
        {
            _hoaDonService = hoaDonService;
            _invoiceViewService = invoiceViewService;
        }

        public IActionResult Index([FromQuery] int? hoaDonId)
        {
            if (!User.IsInRole("KhachThue"))
            {
                return Content("LỖI HỆ THỐNG: Truy cập bị từ chối. Tài khoản của bạn không có quyền Khách Thuê.");
            }

            var nguoiThueIdClaim = User.FindFirst("NguoiThueId");
            if (nguoiThueIdClaim == null) return Content("LỖI HỆ THỐNG: Tài khoản của bạn không được liên kết với hồ sơ người thuê nào (NguoiThueId bị trống). Vui lòng liên hệ Admin.");

            ViewBag.MoHoaDonId = (hoaDonId.HasValue && hoaDonId.Value > 0) ? hoaDonId.Value : 0;

            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetDanhSach()
        {
            var nguoiThueIdClaim = User.FindFirst("NguoiThueId");
            if (nguoiThueIdClaim == null) return Unauthorized();
            int nguoiThueId = int.Parse(nguoiThueIdClaim.Value);

            var hoaDons = await _hoaDonService.GetHoaDonsByNguoiThueIdAsync(nguoiThueId);
            var result = hoaDons.Select(h => new {
                h.HoaDonId,
                h.MaHoaDon,
                h.Thang,
                h.Nam,
                h.TongTien,
                TrangThaiHoaDon = h.TrangThaiHoaDonValue,
                trangThai = h.TrangThaiHoaDon,
                trangThaiPhatHanh = h.TrangThaiPhatHanhValue,
                NgayTao = h.NgayTao
            });

            return Json(result);
        }

        [HttpGet]
        public async Task<IActionResult> XemChiTiet(int id)
        {
            var nguoiThueIdClaim = User.FindFirst("NguoiThueId");
            if (nguoiThueIdClaim == null) return Unauthorized();
            int nguoiThueId = int.Parse(nguoiThueIdClaim.Value);

            var data = await _invoiceViewService.GetTenantDetailAsync(id, nguoiThueId);
            if (data == null) return NotFound("Không tìm thấy hóa đơn hoặc không có quyền truy cập.");

            return Json(new {
                hoaDonId = data.HoaDonId,
                maHoaDon = data.MaHoaDon,
                ngayTao = data.NgayTao,
                tenPhong = data.TenPhong,
                thang = data.Thang,
                nam = data.Nam,
                tenNguoiThue = data.TenNguoiThue,
                trangThaiHoaDon = data.TrangThaiHoaDon,
                trangThaiThanhToanValue = data.TrangThaiThanhToanValue,
                tongTien = data.TongTien,
                daThu = data.DaThu,
                conLai = data.ConLai,
                chiTietHoaDons = data.ChiTietHoaDons,
                lichSuThanhToans = data.LichSuThanhToans,
                daHuy = data.DaHuy,
                lyDoHuy = data.LyDoHuy
            });
        }

        [HttpGet("/KhachThue/HoaDon/TaiPdf/{id:int}")]
        public async Task<IActionResult> TaiPdf(int id)
        {
            var nguoiThueIdClaim = User.FindFirst("NguoiThueId");
            if (nguoiThueIdClaim == null) return Unauthorized();
            if (!int.TryParse(nguoiThueIdClaim.Value, out int nguoiThueId)) return Unauthorized();

            var bytes = await _invoiceViewService.ExportTenantPdfAsync(id, nguoiThueId);
            if (bytes == null) return NotFound("Không tìm thấy hóa đơn hoặc không có quyền truy cập.");

            return File(bytes, "application/pdf", $"HoaDon_{id}.pdf");
        }
    }
}
