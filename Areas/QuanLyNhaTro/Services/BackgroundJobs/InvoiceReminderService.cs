using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Services.Emails;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Services.HoaDons;
using QuanLyChoThuePhongTroWeb.Data;
using QuanLyChoThuePhongTroWeb.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Services.BackgroundJobs
{
    public class InvoiceReminderService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<InvoiceReminderService> _logger;
        private readonly IHostEnvironment _env;
        private readonly object _fileLock = new object();

        public InvoiceReminderService(
            IServiceScopeFactory scopeFactory,
            IConfiguration configuration,
            ILogger<InvoiceReminderService> logger,
            IHostEnvironment env)
        {
            _scopeFactory = scopeFactory;
            _configuration = configuration;
            _logger = logger;
            _env = env;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Dịch vụ tự động gửi mail nhắc nợ (InvoiceReminderService) đã khởi động.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessRemindersAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi xảy ra trong quá trình quét và gửi email nhắc nợ.");
                }

                // Chạy kiểm tra mỗi giờ một lần
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }

            _logger.LogInformation("Dịch vụ tự động gửi mail nhắc nợ (InvoiceReminderService) đang dừng.");
        }

        private async Task ProcessRemindersAsync(CancellationToken stoppingToken)
        {
            // 1. Đọc cấu hình từ appsettings.json
            var enabled = _configuration.GetValue<bool>("AutoReminderSettings:Enabled");
            if (!enabled)
            {
                return;
            }

            var overdueDays = _configuration.GetValue<int>("AutoReminderSettings:OverdueDays", 5);
            var overdueThreshold = DateTime.UtcNow.AddDays(-overdueDays);

            _logger.LogInformation("Bắt đầu quét hóa đơn quá hạn chưa thanh toán (Từ {OverdueDays} ngày trước, NgayTao <= {OverdueThreshold:yyyy-MM-dd HH:mm:ss})...", overdueDays, overdueThreshold);

            // 2. Tải danh sách các hóa đơn đã gửi email nhắc nợ trước đó để tránh trùng lặp
            var sentIds = LoadSentReminders();

            using (var scope = _scopeFactory.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
                var hoaDonService = scope.ServiceProvider.GetRequiredService<IHoaDonService>();

                // 3. Lọc hóa đơn chưa thanh toán, chưa xóa, quá hạn 5 ngày
                var overdueInvoices = await dbContext.HoaDons
                    .Include(h => h.HopDong)
                        .ThenInclude(hd => hd.NguoiThue)
                    .Where(h => !h.IsDeleted 
                        && h.TrangThaiHoaDon == TrangThaiHoaDon.ChuaThanhToan
                        && h.NgayTao <= overdueThreshold)
                    .ToListAsync(stoppingToken);

                _logger.LogInformation("Tìm thấy {Count} hóa đơn quá hạn chưa thanh toán trong hệ thống.", overdueInvoices.Count);

                int sentCount = 0;
                foreach (var invoice in overdueInvoices)
                {
                    if (stoppingToken.IsCancellationRequested)
                        break;

                    // Nếu đã gửi nhắc nợ cho hóa đơn này rồi, bỏ qua
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
                        // Lấy chi tiết hóa đơn (chứa đầy đủ thông tin chi nhánh, dịch vụ) để đưa vào email template
                        var hdDetail = await hoaDonService.GetHoaDonByIdAsync(invoice.HoaDonId);
                        if (hdDetail == null)
                        {
                            _logger.LogWarning("Không thể lấy chi tiết hóa đơn ID {HoaDonId} từ Service.", invoice.HoaDonId);
                            continue;
                        }

                        // Xuất file PDF hóa đơn đính kèm
                        var pdfBytes = await hoaDonService.ExportPdfAsync(invoice.HoaDonId);
                        if (pdfBytes == null || pdfBytes.Length == 0)
                        {
                            _logger.LogError("Không thể xuất file PDF hóa đơn cho mã hóa đơn {MaHoaDon}.", invoice.MaHoaDon);
                            continue;
                        }

                        // Gửi email qua SMTP
                        var result = await emailService.SendInvoiceEmailAsync(customerEmail, hdDetail, pdfBytes);
                        if (result.IsSuccess)
                        {
                            _logger.LogInformation("Gửi email nhắc nợ tự động cho hóa đơn {MaHoaDon} thành công.", invoice.MaHoaDon);
                            SaveSentReminder(invoice.HoaDonId);
                            sentCount++;
                        }
                        else
                        {
                            _logger.LogError("Gửi email nhắc nợ tự động cho hóa đơn {MaHoaDon} thất bại: {Error}", invoice.MaHoaDon, result.ErrorMessage);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Lỗi xảy ra trong quá trình gửi email nhắc nợ cho hóa đơn {MaHoaDon}.", invoice.MaHoaDon);
                    }
                }

                if (sentCount > 0)
                {
                    _logger.LogInformation("Hoàn thành chu kỳ gửi nhắc nợ tự động. Đã gửi thành công: {Count} email.", sentCount);
                }
            }
        }

        private string GetSentRemindersFilePath()
        {
            return Path.Combine(_env.ContentRootPath, "sent_reminders.json");
        }

        private HashSet<int> LoadSentReminders()
        {
            lock (_fileLock)
            {
                var filePath = GetSentRemindersFilePath();
                if (!File.Exists(filePath))
                {
                    return new HashSet<int>();
                }

                try
                {
                    var json = File.ReadAllText(filePath);
                    var list = JsonSerializer.Deserialize<List<int>>(json);
                    return list != null ? new HashSet<int>(list) : new HashSet<int>();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi xảy ra khi đọc tệp sent_reminders.json. Khởi tạo lại danh sách rỗng.");
                    return new HashSet<int>();
                }
            }
        }

        private void SaveSentReminder(int invoiceId)
        {
            lock (_fileLock)
            {
                var filePath = GetSentRemindersFilePath();
                var sentIds = LoadSentReminders();
                if (sentIds.Add(invoiceId))
                {
                    try
                    {
                        var json = JsonSerializer.Serialize(sentIds.ToList(), new JsonSerializerOptions { WriteIndented = true });
                        File.WriteAllText(filePath, json);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Lỗi xảy ra khi lưu tệp sent_reminders.json.");
                    }
                }
            }
        }
    }
}
