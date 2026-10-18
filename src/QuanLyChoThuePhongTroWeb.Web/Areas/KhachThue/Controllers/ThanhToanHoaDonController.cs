using System;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Services;
using QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;
using QuanLyChoThuePhongTroWeb.Models.ThanhToan;
using QuanLyChoThuePhongTroWeb.Web.Helpers;

namespace QuanLyChoThuePhongTroWeb.Areas.KhachThue.Controllers
{
    // Khung "Thanh toán" của khách thuê (spec thanh toán trực tuyến §3.2, §3.4): QR và deeplink theo số còn lại, biên lai web.
    // Khách không còn tự tạo lượt hay nộp minh chứng (spec §8). Quyền sở hữu hóa đơn do Application kiểm.
    [Area("KhachThue")]
    [Authorize(Roles = "KhachThue")]
    [AutoValidateAntiforgeryToken]
    public class ThanhToanHoaDonController : Controller
    {
        private readonly ITenantPaymentPanelService _panel;
        private readonly IInvoiceReceiptService _receipts;
        private readonly IInvoicePaymentRequestService _requests;

        public ThanhToanHoaDonController(
            ITenantPaymentPanelService panel,
            IInvoiceReceiptService receipts,
            IInvoicePaymentRequestService requests)
        {
            _panel = panel;
            _receipts = receipts;
            _requests = requests;
        }

        [HttpGet("/KhachThue/HoaDon/ThanhToan/{hoaDonId:int}")]
        public async Task<IActionResult> Panel(int hoaDonId, CancellationToken ct)
        {
            if (!TryGetTenant(out var nguoiThueId, out _))
            {
                return Unauthorized(new { success = false, message = "Tài khoản chưa liên kết hồ sơ người thuê." });
            }

            var result = await _panel.GetPanelAsync(hoaDonId, nguoiThueId, ct);
            if (!result.Success)
            {
                return this.ToErrorResult(result);
            }

            var p = result.Data!;
            return Ok(new
            {
                success = true,
                data = new
                {
                    hoaDonId = p.HoaDonId,
                    maHoaDon = p.MaHoaDon,
                    tenPhong = p.TenPhong,
                    tenChiNhanh = p.TenChiNhanh,
                    thang = p.Thang,
                    nam = p.Nam,
                    tongTien = p.TongTien,
                    daThu = p.DaThu,
                    conLai = p.ConLai,
                    hanThanhToan = p.HanThanhToanUtc.HasValue ? VietnamTime.Format(p.HanThanhToanUtc.Value, "dd/MM/yyyy") : string.Empty,
                    coTheThanhToan = p.CoTheThanhToan,
                    lyDoKhongTheThanhToan = p.LyDoKhongTheThanhToan,
                    maChuyenKhoan = p.MaChuyenKhoan,
                    tenNganHang = p.TenNganHang,
                    soTaiKhoan = p.SoTaiKhoan,
                    chuTaiKhoan = p.ChuTaiKhoan,
                    soTienSaoChep = p.ConLai.ToString("0", CultureInfo.InvariantCulture),
                    deeplinks = p.Deeplinks.Select(d => new { tenNganHang = d.TenNganHang, url = d.Url }),
                    cacLanThanhToan = p.CacLanThanhToan.Select(e => new
                    {
                        lichSuThanhToanId = e.LichSuThanhToanId,
                        dotThu = e.DotThu,
                        soTien = e.SoTien,
                        ngayThanhToan = VietnamTime.Format(e.NgayThanhToanUtc, "dd/MM/yyyy HH:mm"),
                        phuongThuc = e.PhuongThuc
                    }),
                    minhChungChoDoiChieu = p.MinhChungChoDoiChieu == null
                        ? null
                        : new
                        {
                            minhChungId = p.MinhChungChoDoiChieu.MinhChungId,
                            soTien = p.MinhChungChoDoiChieu.SoTien,
                            ngayNop = VietnamTime.Format(p.MinhChungChoDoiChieu.NgayNopUtc, "dd/MM/yyyy HH:mm")
                        }
                }
            });
        }

        // Không có taiVe: ảnh hiển thị trong <img>. Có taiVe=true: tải về với tên file ASCII.
        [HttpGet("/KhachThue/HoaDon/ThanhToan/QR/{hoaDonId:int}")]
        public async Task<IActionResult> QR(int hoaDonId, [FromQuery] bool taiVe, CancellationToken ct)
        {
            if (!TryGetTenant(out var nguoiThueId, out _))
            {
                return Unauthorized();
            }

            var result = await _panel.GetQrAsync(hoaDonId, nguoiThueId, ct);
            if (!result.Success)
            {
                return this.ToErrorResult(result);
            }

            var qr = result.Data!;
            return taiVe ? File(qr.Png, "image/png", qr.FileName) : File(qr.Png, "image/png");
        }

        [HttpGet("/KhachThue/HoaDon/ThanhToan/AnhMinhChung/{minhChungId:int}")]
        public async Task<IActionResult> AnhMinhChung(int minhChungId, CancellationToken ct)
        {
            if (!TryGetTenant(out var nguoiThueId, out _))
            {
                return Unauthorized();
            }

            var result = await _requests.GetProofImageAsync(minhChungId, nguoiThueId, ct);
            return result.Success ? File(result.Data!.Bytes, result.Data.ContentType) : this.ToErrorResult(result);
        }

        [HttpGet("/KhachThue/HoaDon/BienLai/{lichSuThanhToanId:int}")]
        public async Task<IActionResult> BienLai(int lichSuThanhToanId, CancellationToken ct)
        {
            if (!TryGetTenant(out var nguoiThueId, out _))
            {
                return Unauthorized();
            }

            var result = await _receipts.GetForTenantAsync(lichSuThanhToanId, nguoiThueId, ct);
            if (!result.Success)
            {
                // Trang mở thẳng từ link email nên trả trang lỗi thân thiện, mã HTTP vẫn theo loại lỗi.
                var errorView = View("BienLaiLoi", BienLaiLoiViewModel.From(result, "/KhachThue/HoaDon", "Về danh sách hóa đơn"));
                errorView.StatusCode = result.ToHttpStatusCode();
                return errorView;
            }

            var receipt = result.Data!;
            return View("BienLai", BienLaiThanhToanViewModel.From(receipt, $"/KhachThue/HoaDon?hoaDonId={receipt.HoaDonId}"));
        }

        private bool TryGetTenant(out int nguoiThueId, out int userId)
        {
            userId = 0;
            return int.TryParse(User.FindFirst("NguoiThueId")?.Value, out nguoiThueId) && nguoiThueId > 0 &&
                   int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId) && userId > 0;
        }
    }
}
