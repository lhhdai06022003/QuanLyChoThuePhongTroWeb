using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels;
using System.Globalization;
using System.Security.Claims;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Security;
using QuanLyChoThuePhongTroWeb.Application.Common.Security;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.DTOs;
using Microsoft.Extensions.Logging;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Services;
using QuanLyChoThuePhongTroWeb.Web.Helpers;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Controllers
{
    [ApiController]
    public class HoaDonController : AdminBaseController
    {
        private readonly IHoaDonService _hoaDonService;
        private readonly IPhongTroService _phongTroService;
        private readonly Microsoft.Extensions.Configuration.IConfiguration _configuration;
        private readonly IVietQRService _vietQRService;
        private readonly ILogger<HoaDonController> _logger;
        private readonly IInvoiceIssuanceService _invoiceIssuanceService;
        private readonly IEmployeeAccessService _employeeAccessService;
        private readonly IInvoicePublicationService _invoicePublicationService;
        private readonly IInvoiceViewService _invoiceViewService;
        private readonly IInvoiceLedgerService _ledgerService;
        private readonly IInvoicePaymentConfirmationService _paymentService;

        public HoaDonController(
            IHoaDonService hoaDonService,
            IPhongTroService phongTroService,
            Microsoft.Extensions.Configuration.IConfiguration configuration,
            IVietQRService vietQRService,
            ILogger<HoaDonController> logger,
            IInvoiceIssuanceService invoiceIssuanceService,
            IEmployeeAccessService employeeAccessService,
            IInvoicePublicationService invoicePublicationService,
            IInvoiceViewService invoiceViewService,
            IInvoiceLedgerService ledgerService,
            IInvoicePaymentConfirmationService paymentService)
        {
            _ledgerService = ledgerService;
            _paymentService = paymentService;
            _invoiceIssuanceService = invoiceIssuanceService;
            _hoaDonService = hoaDonService;
            _phongTroService = phongTroService;
            _configuration = configuration;
            _vietQRService = vietQRService;
            _logger = logger;
            _employeeAccessService = employeeAccessService;
            _invoicePublicationService = invoicePublicationService;
            _invoiceViewService = invoiceViewService;
        }

        [Route("QuanLyNhaTro/QuanLyHoaDon")]
        [ApiExplorerSettings(IgnoreApi = true)]
        public async Task<IActionResult> Index()
        {
            ViewBag.ListChiNhanh = await _phongTroService.GetDanhSachChiNhanhDropdownAsync();
            ViewBag.BankId = _configuration["VietQRSettings:BankId"] ?? "MB";
            ViewBag.AccountNumber = _configuration["VietQRSettings:AccountNumber"] ?? "";
            ViewBag.AccountName = _configuration["VietQRSettings:AccountName"] ?? "";

            var chuaPhanCong = false;
            if (User.IsInRole("NhanVien") && int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId))
            {
                var scope = await _employeeAccessService.GetScopeAsync(actorId);
                chuaPhanCong = scope != null && !scope.IsAdmin && scope.ActiveBranchIds.Count == 0;
            }
            ViewBag.ChuaPhanCongChiNhanh = chuaPhanCong;

            return View();
        }

        // Phát sinh hóa đơn hàng loạt hoặc theo phòng đã chọn
        [HttpPost("/HoaDon/PhatSinh")]
        public async Task<IActionResult> PhatSinh([FromBody] PhatSinhHoaDonReq req)
        {
            if (req.ChiNhanhId <= 0 || req.Thang < 1 || req.Thang > 12 || req.Nam < 2000)
                return BadRequest(new { Message = "Tham số không hợp lệ." });

            var actorIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(actorIdStr, out var actorId) || actorId <= 0)
            {
                return Unauthorized(new { Message = "Người dùng chưa đăng nhập hoặc phiên làm việc đã hết hạn." });
            }

            var request = new CreateInvoiceDraftsRequest
            {
                ChiNhanhId = req.ChiNhanhId,
                Thang = req.Thang,
                Nam = req.Nam,
                PhongTroIds = req.SelectedPhongTroIds ?? new List<int>()
            };

            var result = await _invoiceIssuanceService.CreateDraftsAsync(request, actorId);
            if (!result.Success)
            {
                return BadRequest(new { Message = result.Message });
            }

            var batch = result.Data!;
            var successes = batch.Items
                .Where(x => x.Status == InvoiceDraftItemStatus.Created)
                .Select(x => x.Message)
                .ToList();
            var skipped = batch.Items
                .Where(x => x.Status != InvoiceDraftItemStatus.Created)
                .Select(x => x.Message)
                .ToList();

            var message = $"Phát sinh thành công {batch.CreatedCount} hóa đơn nháp.";
            if (skipped.Any())
            {
                message += $" Bỏ qua {skipped.Count} hợp đồng (xem chi tiết).";
            }

            return Ok(new
            {
                IsSuccess = true,
                Message = message,
                SoHoaDonMoi = batch.CreatedCount,
                Skipped = skipped,
                Successes = successes
            });
        }

        // Xem trước phát sinh hóa đơn
        [HttpGet("/HoaDon/PreviewPhatSinh")]
        public async Task<IActionResult> PreviewPhatSinh([FromQuery] int chiNhanhId, [FromQuery] int thang, [FromQuery] int nam)
        {
            if (chiNhanhId <= 0 || thang < 1 || thang > 12 || nam < 2000)
                return BadRequest(new { Message = "Tham số không hợp lệ." });

            if (!TryGetActorId(out var actorId))
                return Unauthorized(new { Message = "Người dùng chưa đăng nhập hoặc phiên làm việc đã hết hạn." });

            var data = await _hoaDonService.PreviewPhatSinhHoaDonAsync(chiNhanhId, thang, nam, actorId);
            return Ok(data);
        }

        // Cập nhật/Chỉnh sửa hóa đơn
        [HttpPost("/HoaDon/Update/{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateHoaDonReq req)
        {
            if (id <= 0 || req == null)
                return BadRequest(new { Message = "Dữ liệu không hợp lệ." });

            var actorIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(actorIdStr, out var actorId) || actorId <= 0)
            {
                return Unauthorized(new { Message = "Người dùng chưa đăng nhập hoặc phiên làm việc đã hết hạn." });
            }

            var result = await _hoaDonService.UpdateHoaDonAsync(id, req, actorId);
            if (!result.IsSuccess)
                return BadRequest(new { Message = result.ErrorMessage });

            return Ok(new { Message = "Cập nhật hóa đơn thành công!" });
        }

        // Danh sách hóa đơn (DataTable server-side)
        [HttpPost("/HoaDon/GetList")]
        public async Task<IActionResult> GetList(
            [FromForm] int chiNhanhId = 0,
            [FromForm] int thang = 0,
            [FromForm] int nam = 0,
            [FromForm] int trangThai = -1,
            [FromForm] int trangThaiPhatHanh = -1)
        {
            var form = Request.Form;
            var request = new DataTableRequest
            {
                Draw = int.TryParse(form["draw"].FirstOrDefault(), out int d) ? d : 1,
                Start = int.TryParse(form["start"].FirstOrDefault(), out int s) ? s : 0,
                Length = int.TryParse(form["length"].FirstOrDefault(), out int l) ? l : 10,
                SearchValue = form["search[value]"].FirstOrDefault()
            };

            if (!TryGetActorId(out var actorId))
                return Unauthorized(new { Message = "Người dùng chưa đăng nhập hoặc phiên làm việc đã hết hạn." });

            var filter = new InvoiceListFilter
            {
                ChiNhanhId = chiNhanhId,
                Thang = thang,
                Nam = nam,
                TrangThaiThanhToan = trangThai,
                TrangThaiPhatHanh = trangThaiPhatHanh
            };

            var data = await _invoiceViewService.GetEmployeeListAsync(request, filter, actorId);
            return Ok(data);
        }

        // Chi tiết hóa đơn
        [HttpGet("/HoaDon/GetById/{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            if (id <= 0) return BadRequest(new { Message = "ID hóa đơn không hợp lệ." });

            if (!TryGetActorId(out var actorId))
                return Unauthorized(new { Message = "Người dùng chưa đăng nhập hoặc phiên làm việc đã hết hạn." });

            var data = await _invoiceViewService.GetEmployeeDetailAsync(id, actorId);
            if (data == null) return NotFound(new { Message = "Không tìm thấy hóa đơn hoặc bạn không có quyền truy cập." });
            return Ok(data);
        }

        // Thu tiền thủ công theo số thực nhận (spec thanh toán §18)
        [HttpPost("/HoaDon/ThuTien")]
        public async Task<IActionResult> ThuTien([FromBody] ThuTienFormReq req, CancellationToken ct)
        {
            if (req == null || req.HoaDonId <= 0) return BadRequest(new { Message = "Dữ liệu hóa đơn không hợp lệ." });
            if (!TryGetActorId(out var actorId))
            {
                return Unauthorized(new { Message = "Người dùng chưa đăng nhập hoặc phiên làm việc đã hết hạn." });
            }

            if (!VietnamTime.TryParseToUtc(req.NgayThanhToan, out var ngayThanhToanUtc))
            {
                return BadRequest(new { Message = "Vui lòng nhập ngày thanh toán hợp lệ." });
            }

            var result = await _ledgerService.CollectManuallyAsync(new ManualCollectionRequest
            {
                HoaDonId = req.HoaDonId,
                SoTienThucNhan = req.SoTienThucNhan,
                PhuongThuc = (AppPhuongThucThanhToan)req.PhuongThucThanhToan,
                NgayThanhToanUtc = ngayThanhToanUtc,
                MaGiaoDich = req.MaGiaoDich,
                GhiChu = req.GhiChu
            }, actorId, ct);

            if (!result.Success) return this.ToErrorResult(result);
            var data = result.Data!;
            var message = data.ConLai > 0
                ? $"Đã ghi nhận {data.SoTienGhiNhan:#,##0}đ, hóa đơn còn nợ {data.ConLai:#,##0}đ."
                : "Thu tiền thành công, hóa đơn đã thanh toán đủ.";
            if (data.MaLuotDaHuy != null)
            {
                message += $" Lượt {data.MaLuotDaHuy} của khách đã được hủy.";
            }

            return Ok(new { Message = message, data });
        }

        // Tóm tắt thanh toán cho modal chi tiết: số còn lại, lượt đang hoạt động, cấu hình trả một phần
        [HttpGet("/HoaDon/ThanhToanTomTat/{id:int}")]
        public async Task<IActionResult> ThanhToanTomTat(int id, CancellationToken ct)
        {
            if (!TryGetActorId(out var actorId))
                return Unauthorized(new { Message = "Người dùng chưa đăng nhập hoặc phiên làm việc đã hết hạn." });

            var result = await _paymentService.GetInvoicePaymentSummaryAsync(id, actorId, ct);
            return result.Success ? Ok(result.Data) : this.ToErrorResult(result);
        }

        // Bật/tắt thanh toán một phần, chỉ Admin (spec thanh toán §19)
        [HttpPost("/HoaDon/CauHinhThanhToanMotPhan/{id:int}")]
        public async Task<IActionResult> CauHinhThanhToanMotPhan(int id, [FromBody] CauHinhMotPhanReq req, CancellationToken ct)
        {
            if (!TryGetActorId(out var actorId))
                return Unauthorized(new { Message = "Người dùng chưa đăng nhập hoặc phiên làm việc đã hết hạn." });

            var result = await _paymentService.ConfigurePartialPaymentAsync(new ConfigurePartialPaymentRequest
            {
                HoaDonId = id,
                ChoPhep = req?.ChoPhep ?? false,
                SoTienToiThieu = req?.SoTienToiThieu
            }, actorId, ct);

            return result.Success ? Ok(new { Message = result.Message, data = result.Data }) : this.ToErrorResult(result);
        }

        // Hủy hóa đơn (spec thanh toán §31.2: tự hủy lượt chờ thanh toán, chặn khi đang đối chiếu)
        [HttpDelete("/HoaDon/Delete/{id}")]
        public async Task<IActionResult> Delete(int id, [FromBody] CancelInvoiceReq req, CancellationToken ct)
        {
            if (id <= 0) return BadRequest(new { Message = "ID hóa đơn không hợp lệ." });
            if (req == null || string.IsNullOrWhiteSpace(req.LyDo))
            {
                return BadRequest(new { Message = "Vui lòng nhập lý do hủy hóa đơn." });
            }

            if (!TryGetActorId(out var actorId))
            {
                return Unauthorized(new { Message = "Người dùng chưa đăng nhập hoặc phiên làm việc đã hết hạn." });
            }

            var result = await _ledgerService.CancelInvoiceAsync(id, actorId, req.LyDo, ct);
            if (!result.Success) return this.ToErrorResult(result);
            return Ok(new { Message = "Đã hủy hóa đơn thành công." });
        }

        // Xuất Excel
        [HttpGet("/HoaDon/ExportExcel/{id}")]
        [ApiExplorerSettings(IgnoreApi = true)]
        public async Task<IActionResult> ExportExcel(int id)
        {
            if (id <= 0) return BadRequest(new { Message = "ID hóa đơn không hợp lệ." });

            if (!TryGetActorId(out var actorId))
                return Unauthorized(new { Message = "Người dùng chưa đăng nhập hoặc phiên làm việc đã hết hạn." });

            var bytes = await _invoiceViewService.ExportEmployeeExcelAsync(id, actorId);
            if (bytes == null) return NotFound();
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"HoaDon_{id}.xlsx");
        }

        // Xuất PDF
        [HttpGet("/HoaDon/ExportPdf/{id}")]
        [ApiExplorerSettings(IgnoreApi = true)]
        public async Task<IActionResult> ExportPdf(int id)
        {
            if (id <= 0) return BadRequest(new { Message = "ID hóa đơn không hợp lệ." });

            if (!TryGetActorId(out var actorId))
                return Unauthorized(new { Message = "Người dùng chưa đăng nhập hoặc phiên làm việc đã hết hạn." });

            var bytes = await _invoiceViewService.ExportEmployeePdfAsync(id, actorId);
            if (bytes == null) return NotFound();
            return File(bytes, "application/pdf", $"HoaDon_{id}.pdf");
        }

        // Endpoint sinh mã QR VietQR ngoại tuyến
        [HttpGet("/HoaDon/GetVietQR")]
        public async Task<IActionResult> GetVietQR([FromQuery] int hoaDonId)
        {
            if (!TryGetActorId(out var actorId))
                return Unauthorized(new { Message = "Người dùng chưa đăng nhập hoặc phiên làm việc đã hết hạn." });

            var hd = await _hoaDonService.GetEmployeeInvoiceDetailAsync(hoaDonId, actorId);
            if (hd == null) return NotFound(new { Message = "Không tìm thấy hóa đơn hoặc bạn không có quyền truy cập." });

            // QR theo số còn lại; chỉ khi hóa đơn còn nợ
            if (hd.DaHuy || hd.ConLai <= 0)
            {
                return BadRequest(new { Message = "Hóa đơn đã được thanh toán hoặc không hợp lệ." });
            }

            var bankId = _configuration["VietQRSettings:BankId"] ?? "MB";
            var accountNumber = _configuration["VietQRSettings:AccountNumber"] ?? "";
            string memo = $"THANH TOAN {hd.MaHoaDon}";

            string qrString = _vietQRService.GenerateVietQRString(bankId, accountNumber, hd.ConLai, memo);
            byte[] qrBytes = _vietQRService.GenerateQRCodePNGBytes(qrString);

            return File(qrBytes, "image/png");
        }

        // Lấy danh sách hóa đơn chưa thanh toán để gửi mail hàng loạt
        [HttpGet("/HoaDon/GetUnpaidList")]
        public async Task<IActionResult> GetUnpaidList([FromQuery] int chiNhanhId, [FromQuery] int thang, [FromQuery] int nam)
        {
            try
            {
                if (!TryGetActorId(out var actorId))
                    return Unauthorized(new { Message = "Người dùng chưa đăng nhập hoặc phiên làm việc đã hết hạn." });

                var data = await _hoaDonService.GetEmployeeUnpaidInvoicesAsync(chiNhanhId, thang, nam, actorId);
                return Ok(data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi tải danh sách hóa đơn chưa thanh toán.");
                return StatusCode(500, new { Message = "Lỗi hệ thống khi tải danh sách hóa đơn chưa thanh toán." });
            }
        }

        // Công bố hóa đơn lên cổng khách thuê và gửi email
        [HttpPost("/HoaDon/CongBo")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CongBo([FromBody] PublishInvoicesReq? req)
        {
            var hoaDonIds = req?.HoaDonIds;
            if (hoaDonIds == null || hoaDonIds.Count == 0)
            {
                return BadRequest(new { Message = "Vui lòng chọn ít nhất một hóa đơn để công bố." });
            }

            if (hoaDonIds.Any(id => id <= 0))
            {
                return BadRequest(new { Message = "Mã hóa đơn không hợp lệ." });
            }

            if (hoaDonIds.Distinct().Count() > 50)
            {
                return BadRequest(new { Message = "Mỗi lần chỉ được công bố tối đa 50 hóa đơn." });
            }

            if (!TryGetActorId(out var actorId))
            {
                return Unauthorized(new { Message = "Người dùng chưa đăng nhập hoặc phiên làm việc đã hết hạn." });
            }

            var result = await _invoicePublicationService.PublishAsync(new PublishInvoicesRequest { HoaDonIds = hoaDonIds }, actorId);
            if (!result.Success)
            {
                return BadRequest(new { Message = result.Message });
            }

            var batch = result.Data!;

            return Ok(new
            {
                Items = batch.Items.Select(MapPublishItem),
                SoDaCongBo = batch.PublishedCount
            });
        }

        private static object MapPublishItem(InvoicePublishItemResult item)
        {
            return new
            {
                item.HoaDonId,
                item.MaHoaDon,
                CongBo = MapPublishOutcomeMessage(item),
                CongBoThanhCong = item.Publish == InvoicePublishOutcome.Published || item.Publish == InvoicePublishOutcome.AlreadyPublished,
                ThongBaoCong = MapPortalNotification(item.PortalNotification),
                Email = MapEmailOutcome(item.Email),
                EmailChiTiet = item.EmailMessage,
                HanThanhToan = item.HanThanhToanUtc?.AddHours(7).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)
            };
        }

        private static string MapPublishOutcomeMessage(InvoicePublishItemResult item)
        {
            return item.Publish switch
            {
                InvoicePublishOutcome.Published => "Đã công bố",
                InvoicePublishOutcome.AlreadyPublished => "Đã công bố trước đó",
                _ => item.PublishMessage
            };
        }

        private static string MapPortalNotification(InvoicePortalNotificationOutcome outcome)
        {
            return outcome switch
            {
                InvoicePortalNotificationOutcome.Created => "Đã tạo thông báo",
                InvoicePortalNotificationOutcome.NoPortalAccount => "Khách chưa có tài khoản cổng",
                _ => ""
            };
        }

        private static string MapEmailOutcome(InvoiceEmailOutcome outcome)
        {
            return outcome switch
            {
                InvoiceEmailOutcome.Sent => "Đã gửi",
                InvoiceEmailOutcome.Failed => "Lỗi gửi",
                InvoiceEmailOutcome.NoEmail => "Không có email",
                _ => "Không gửi"
            };
        }

        // Gửi lại email hóa đơn
        [HttpPost("/HoaDon/SendEmail/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendEmail(int id)
        {
            if (id <= 0) return BadRequest(new { Message = "ID hóa đơn không hợp lệ." });

            if (!TryGetActorId(out var actorId))
            {
                return Unauthorized(new { Message = "Người dùng chưa đăng nhập hoặc phiên làm việc đã hết hạn." });
            }

            var result = await _invoicePublicationService.ResendEmailAsync(id, actorId);
            if (!result.Success)
            {
                return BadRequest(new { Message = result.Message });
            }

            var data = result.Data!;
            var message = data.Email switch
            {
                InvoiceEmailOutcome.Sent => "Đã gửi lại email hóa đơn.",
                InvoiceEmailOutcome.Failed => "Gửi email thất bại. Vui lòng thử lại sau.",
                InvoiceEmailOutcome.NoEmail => "Khách thuê chưa có địa chỉ email.",
                _ => data.EmailMessage
            };

            return Ok(new
            {
                Message = message,
                Email = MapEmailOutcome(data.Email),
                data.RecipientEmail,
                InvoiceCode = data.MaHoaDon
            });
        }

        [HttpPost("/HoaDon/GuiDuyet/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GuiDuyet(int id)
        {
            if (id <= 0) return BadRequest(new { success = false, message = "ID hóa đơn không hợp lệ." });

            if (!TryGetActorId(out var actorId))
                return Unauthorized(new { success = false, message = "Người dùng chưa đăng nhập hoặc phiên làm việc đã hết hạn." });

            var result = await _invoiceIssuanceService.SubmitAsync(id, actorId);
            if (!result.Success)
            {
                return BadRequest(new { success = false, message = result.Message });
            }

            return Ok(new
            {
                success = true,
                message = result.Message,
                trangThaiPhatHanh = (int)result.Data!.TrangThaiPhatHanh
            });
        }

        [HttpPost("/HoaDon/Chot/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Chot(int id)
        {
            if (id <= 0) return BadRequest(new { success = false, message = "ID hóa đơn không hợp lệ." });

            if (!TryGetActorId(out var actorId))
                return Unauthorized(new { success = false, message = "Người dùng chưa đăng nhập hoặc phiên làm việc đã hết hạn." });

            var result = await _invoiceIssuanceService.FinalizeAsync(id, actorId);
            if (!result.Success)
            {
                return BadRequest(new { success = false, message = result.Message });
            }

            return Ok(new
            {
                success = true,
                message = result.Message,
                trangThaiPhatHanh = (int)result.Data!.TrangThaiPhatHanh
            });
        }

        [HttpPost("/HoaDon/TraLai/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TraLai(int id, [FromBody] RejectInvoiceReq? req)
        {
            if (id <= 0) return BadRequest(new { success = false, message = "ID hóa đơn không hợp lệ." });

            if (!TryGetActorId(out var actorId))
                return Unauthorized(new { success = false, message = "Người dùng chưa đăng nhập hoặc phiên làm việc đã hết hạn." });

            var lyDo = req?.LyDo?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(lyDo))
            {
                return BadRequest(new { success = false, message = "Lý do trả lại là bắt buộc." });
            }

            var result = await _invoiceIssuanceService.RejectAsync(id, lyDo, actorId);
            if (!result.Success)
            {
                return BadRequest(new { success = false, message = result.Message });
            }

            return Ok(new
            {
                success = true,
                message = result.Message,
                trangThaiPhatHanh = (int)result.Data!.TrangThaiPhatHanh
            });
        }

        private bool TryGetActorId(out int actorId)
        {
            return int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out actorId) && actorId > 0;
        }
    }

    public class RejectInvoiceReq
    {
        public string? LyDo { get; set; }
    }

    public class PublishInvoicesReq
    {
        public List<int>? HoaDonIds { get; set; }
    }

    public class ThuTienFormReq
    {
        public int HoaDonId { get; set; }
        public decimal SoTienThucNhan { get; set; }
        public int PhuongThucThanhToan { get; set; } // 0 = Tiền mặt, 1 = Chuyển khoản
        // Giờ Việt Nam, ví dụ "2026-10-03T14:30".
        public string? NgayThanhToan { get; set; }
        public string? MaGiaoDich { get; set; }
        public string? GhiChu { get; set; }
    }

    public class CauHinhMotPhanReq
    {
        public bool ChoPhep { get; set; }
        public decimal? SoTienToiThieu { get; set; }
    }
}

