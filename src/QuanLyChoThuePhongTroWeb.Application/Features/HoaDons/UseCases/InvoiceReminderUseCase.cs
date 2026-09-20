using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.BackgroundJobs;
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
        private readonly IInvoiceReminderStateStore _stateStore;
        private readonly ILogger<InvoiceReminderUseCase> _logger;

        public InvoiceReminderUseCase(
            IHoaDonStore store,
            IEmailService emailService,
            IHoaDonService hoaDonService,
            AutoReminderSettings settings,
            IInvoiceReminderStateStore stateStore,
            ILogger<InvoiceReminderUseCase> logger)
        {
            _store = store;
            _emailService = emailService;
            _hoaDonService = hoaDonService;
            _settings = settings;
            _stateStore = stateStore;
            _logger = logger;
        }

        public async Task<int> ExecuteAsync(CancellationToken cancellationToken = default)
        {
            if (!_settings.Enabled)
            {
                return 0;
            }

            var overdueDays = _settings.OverdueDays;
            var overdueThreshold = DateTime.UtcNow.AddDays(-overdueDays);

            _logger.LogInformation("Bắt đầu quét hóa đơn quá hạn chưa thanh toán (Từ {OverdueDays} ngày trước, NgayTao <= {OverdueThreshold:yyyy-MM-dd HH:mm:ss})...", overdueDays, overdueThreshold);

            var sentIds = await _stateStore.GetSentInvoiceIdsAsync(cancellationToken);

            var overdueInvoices = await _store.GetOverdueInvoicesAsync(overdueThreshold, cancellationToken);

            _logger.LogInformation("Tìm thấy {Count} hóa đơn quá hạn chưa thanh toán trong hệ thống.", overdueInvoices.Count);

            int sentCount = 0;
            foreach (var invoice in overdueInvoices)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                if (sentIds.Contains(invoice.HoaDonId))
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
                        await _stateStore.MarkAsSentAsync(invoice.HoaDonId, cancellationToken);
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
    }
}
