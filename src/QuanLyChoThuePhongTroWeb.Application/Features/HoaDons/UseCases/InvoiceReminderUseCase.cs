using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Services;
using QuanLyChoThuePhongTroWeb.Application.Common.Configurations;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Services;

namespace QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.UseCases
{
    public class InvoiceReminderUseCase : IInvoiceReminderUseCase
    {
        private readonly IHoaDonStore _store;
        private readonly IEmailService _emailService;
        private readonly IHoaDonService _hoaDonService;
        private readonly AutoReminderSettings _settings;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<InvoiceReminderUseCase> _logger;

        public InvoiceReminderUseCase(
            IHoaDonStore store,
            IEmailService emailService,
            IHoaDonService hoaDonService,
            AutoReminderSettings settings,
            TimeProvider timeProvider,
            ILogger<InvoiceReminderUseCase> logger)
        {
            _store = store;
            _emailService = emailService;
            _hoaDonService = hoaDonService;
            _settings = settings;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task<int> ExecuteAsync(CancellationToken cancellationToken = default)
        {
            if (!_settings.Enabled)
            {
                return 0;
            }

            // Không có sổ "đã gửi": job chạy mỗi ngày đúng một lượt (DailyJobSchedule) và chỉ gửi vào ngày
            // có mốc quá hạn OverdueDays, OverdueDays + RepeatEveryDays, ... nên deploy lại không gửi trùng.
            var overdueDays = _settings.OverdueDays;
            var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
            var homNayVn = nowUtc.AddHours(7).Date;
            var ngayMaiVnBatDauUtc = DateTime.SpecifyKind(homNayVn.AddDays(1).AddHours(-7), DateTimeKind.Utc);
            // Mọi hóa đơn quá hạn từ overdueDays ngày (tính theo ngày VN) đều có hạn trước mốc này; lọc mốc chính xác ở dưới.
            var overdueThreshold = ngayMaiVnBatDauUtc.AddDays(-overdueDays);

            _logger.LogInformation("Bắt đầu quét hóa đơn quá hạn chưa thanh toán (mốc nhắc từ {OverdueDays} ngày, lặp mỗi {RepeatEveryDays} ngày; HanThanhToan (hoặc NgayGui nếu thiếu hạn) <= {OverdueThreshold:yyyy-MM-dd HH:mm:ss})...", overdueDays, _settings.RepeatEveryDays, overdueThreshold);

            var overdueInvoices = await _store.GetOverdueInvoicesAsync(overdueThreshold, cancellationToken);

            _logger.LogInformation("Tìm thấy {Count} hóa đơn quá hạn chưa thanh toán trong hệ thống.", overdueInvoices.Count);

            int sentCount = 0;
            foreach (var invoice in overdueInvoices)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                var hanUtc = invoice.HanThanhToan ?? invoice.NgayGui;
                if (hanUtc is null || !LaNgayNhac((homNayVn - hanUtc.Value.AddHours(7).Date).Days))
                {
                    continue;
                }

                var customerEmail = invoice.HopDong?.NguoiThue?.Email;
                if (string.IsNullOrWhiteSpace(customerEmail))
                {
                    _logger.LogWarning("Bỏ qua hóa đơn {MaHoaDon} (ID: {HoaDonId}): Khách thuê không có địa chỉ email hợp lệ.", invoice.MaHoaDon, invoice.HoaDonId);
                    continue;
                }

                _logger.LogInformation("Bắt đầu xử lý gửi email tự động cho hóa đơn {MaHoaDon} tới {Email}...", invoice.MaHoaDon, customerEmail);

                try
                {
                    var hdDetail = await _hoaDonService.GetHoaDonByIdAsync(invoice.HoaDonId);
                    if (hdDetail == null)
                    {
                        _logger.LogWarning("Không thể lấy chi tiết hóa đơn ID {HoaDonId} từ Service.", invoice.HoaDonId);
                        continue;
                    }

                    var pdfBytes = await _hoaDonService.ExportPdfAsync(invoice.HoaDonId);
                    if (pdfBytes == null || pdfBytes.Length == 0)
                    {
                        _logger.LogError("Không thể xuất file PDF hóa đơn cho mã hóa đơn {MaHoaDon}.", invoice.MaHoaDon);
                        continue;
                    }

                    var result = await _emailService.SendInvoiceEmailAsync(customerEmail, hdDetail, pdfBytes);
                    if (result.IsSuccess)
                    {
                        _logger.LogInformation("Gửi email nhắc nợ tự động cho hóa đơn {MaHoaDon} thành công.", invoice.MaHoaDon);
                        sentCount++;
                    }
                    else
                    {
                        _logger.LogError("Gửi email nhắc nợ tự động cho hóa đơn {MaHoaDon} thất bại: {ErrorMessage}", invoice.MaHoaDon, result.ErrorMessage);
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi xảy ra trong quá trình xử lý gửi email cho hóa đơn {MaHoaDon}.", invoice.MaHoaDon);
                }
            }

            _logger.LogInformation("Hoàn tất tiến trình gửi email nhắc nợ tự động. Tổng cộng {SentCount} email đã được gửi thành công.", sentCount);
            return sentCount;
        }

        // Ngày quá hạn thứ OverdueDays là mốc đầu; RepeatEveryDays > 0 thì nhắc tiếp mỗi RepeatEveryDays ngày tới khi trả.
        private bool LaNgayNhac(int soNgayQuaHan)
        {
            var sauMocDau = soNgayQuaHan - _settings.OverdueDays;
            if (sauMocDau < 0)
            {
                return false;
            }

            return _settings.RepeatEveryDays > 0
                ? sauMocDau % _settings.RepeatEveryDays == 0
                : sauMocDau == 0;
        }
    }
}
