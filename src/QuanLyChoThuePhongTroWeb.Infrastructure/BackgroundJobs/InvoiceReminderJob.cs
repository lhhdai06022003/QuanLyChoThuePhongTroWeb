using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.UseCases;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.BackgroundJobs
{
    public class InvoiceReminderJob : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<InvoiceReminderJob> _logger;

        public InvoiceReminderJob(
            IServiceScopeFactory scopeFactory,
            ILogger<InvoiceReminderJob> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Dịch vụ lập lịch tự động gửi mail nhắc nợ (InvoiceReminderJob) đã khởi động.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var useCase = scope.ServiceProvider.GetRequiredService<IInvoiceReminderUseCase>();
                    var sentCount = await useCase.ExecuteAsync(stoppingToken);
                    if (sentCount > 0)
                    {
                        _logger.LogInformation("InvoiceReminderJob đã gửi thành công {Count} email nhắc nợ.", sentCount);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi xảy ra trong quá trình quét và gửi email nhắc nợ của InvoiceReminderJob.");
                }

                if (stoppingToken.IsCancellationRequested)
                {
                    break;
                }

                // Chạy kiểm tra mỗi giờ một lần
                var timeToWait = TimeSpan.FromHours(1);
                _logger.LogInformation("InvoiceReminderJob sẽ chạy lại sau: {WaitTime}", timeToWait);
                try
                {
                    await Task.Delay(timeToWait, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }

            _logger.LogInformation("Dịch vụ lập lịch tự động gửi mail nhắc nợ (InvoiceReminderJob) đang dừng.");
        }
    }
}
