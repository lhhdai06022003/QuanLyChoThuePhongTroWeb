using System;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuanLyChoThuePhongTroWeb.Application.Common.Configurations;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Services;
using QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;
using QuanLyChoThuePhongTroWeb.Models.ThanhToan;
using QuanLyChoThuePhongTroWeb.Web.Helpers;

namespace QuanLyChoThuePhongTroWeb.Areas.KhachThue.Controllers
{
    // Khung "Thanh toán" của khách thuê (spec thanh toán trực tuyến §3.2, §3.4): QR và deeplink theo số còn lại, biên lai web.
    // Khách không còn tự tạo lượt hay nộp minh chứng (spec §8). Quyền sở hữu hóa đơn do Application kiểm.
    // Thanh toán thẻ qua VNPay (spec VNPay §4.2, §4.3): tạo link và trang kết quả chỉ đọc; chỉ IPN ghi sổ.
    [Area("KhachThue")]
    [Authorize(Roles = "KhachThue")]
    [AutoValidateAntiforgeryToken]
    public class ThanhToanHoaDonController : Controller
    {
        private readonly ITenantPaymentPanelService _panel;
        private readonly IInvoiceReceiptService _receipts;
        private readonly IInvoicePaymentRequestService _requests;
        private readonly IVnPayPaymentService _vnPay;
        private readonly VnPayOptions _vnPayOptions;

        public ThanhToanHoaDonController(
            ITenantPaymentPanelService panel,
            IInvoiceReceiptService receipts,
            IInvoicePaymentRequestService requests,
            IVnPayPaymentService vnPay,
            VnPayOptions vnPayOptions)
        {
            _panel = panel;
            _receipts = receipts;
            _requests = requests;
            _vnPay = vnPay;
            _vnPayOptions = vnPayOptions;
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
                    vnPayEnabled = _vnPayOptions.Enabled,
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

        // Tạo link VNPay với số tiền bằng số còn lại. Không ghi gì vào DB; ghi sổ do IPN.
        [HttpPost("/KhachThue/HoaDon/ThanhToan/VnPay/{hoaDonId:int}")]
        public async Task<IActionResult> VnPay(int hoaDonId, CancellationToken ct)
        {
            if (!_vnPayOptions.Enabled)
            {
                return NotFound();
            }

            if (!TryGetTenant(out var nguoiThueId, out _))
            {
                return Unauthorized(new { success = false, message = "Tài khoản chưa liên kết hồ sơ người thuê." });
            }

            var result = await _vnPay.CreatePaymentUrlAsync(hoaDonId, nguoiThueId, ClientIp(), BuildReturnUrl(), ct);
            return result.Success ? Ok(new { success = true, url = result.Data }) : this.ToErrorResult(result);
        }

        // VNPay đưa khách về đây; luôn trả trang (HTTP 200) vì sai chữ ký cũng là một trạng thái để hiển thị.
        [HttpGet("/KhachThue/HoaDon/ThanhToan/VnPay/KetQua")]
        public async Task<IActionResult> VnPayKetQua(CancellationToken ct)
        {
            if (!_vnPayOptions.Enabled)
            {
                return NotFound();
            }

            if (!TryGetTenant(out var nguoiThueId, out _))
            {
                return View("VnPayKetQua", VnPayKetQuaViewModel.From(VnPayReturnResult.Unverified));
            }

            var query = Request.Query.ToDictionary(q => q.Key, q => q.Value.ToString(), StringComparer.Ordinal);
            var result = await _vnPay.GetReturnResultAsync(query, nguoiThueId, ct);
            return View("VnPayKetQua", VnPayKetQuaViewModel.From(result));
        }

        // Địa chỉ trang kết quả theo host của request hiện tại (spec VNPay §5.1).
        private string BuildReturnUrl()
        {
            return $"{Request.Scheme}://{Request.Host}{Request.PathBase}/KhachThue/HoaDon/ThanhToan/VnPay/KetQua";
        }

        // IP kết nối của khách (spec VNPay §13). Thiếu hoặc IPv6 loopback thì dùng 127.0.0.1 vì VNPay chỉ nhận IPv4 hoặc IPv6 đầy đủ.
        private string ClientIp()
        {
            var address = HttpContext.Connection.RemoteIpAddress;
            if (address == null || (IPAddress.IsLoopback(address) && address.AddressFamily == AddressFamily.InterNetworkV6))
            {
                return "127.0.0.1";
            }

            return address.IsIPv4MappedToIPv6 ? address.MapToIPv4().ToString() : address.ToString();
        }

        private bool TryGetTenant(out int nguoiThueId, out int userId)
        {
            userId = 0;
            return int.TryParse(User.FindFirst("NguoiThueId")?.Value, out nguoiThueId) && nguoiThueId > 0 &&
                   int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId) && userId > 0;
        }
    }
}
