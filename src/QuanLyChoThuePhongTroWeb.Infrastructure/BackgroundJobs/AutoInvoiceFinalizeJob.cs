using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using QuanLyChoThuePhongTroWeb.Application.Common.Configurations;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.UseCases;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.BackgroundJobs
{
    public class AutoInvoiceFinalizeJob : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly AutoInvoiceOptions _options;
        private readonly ILogger<AutoInvoiceFinalizeJob> _logger;

        public AutoInvoiceFinalizeJob(
            IServiceScopeFactory scopeFactory,
            AutoInvoiceOptions options,
            ILogger<AutoInvoiceFinalizeJob> logger)
        {
            _scopeFactory = scopeFactory;
            _options = options;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Dịch vụ lập lịch tự chốt hóa đơn tạm tính (AutoInvoiceFinalizeJob) đã khởi động.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var useCase = scope.ServiceProvider.GetRequiredService<IAutoInvoiceFinalizeUseCase>();
                    var finalizedCount = await useCase.ExecuteAsync(stoppingToken);
                    if (finalizedCount > 0)
                    {
                        _logger.LogInformation("AutoInvoiceFinalizeJob đã tự chốt {Count} hóa đơn.", finalizedCount);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi xảy ra trong quá trình tự chốt hóa đơn tạm tính của AutoInvoiceFinalizeJob.");
                }

                if (stoppingToken.IsCancellationRequested)
                {
                    break;
                }

                var timeToWait = TimeSpan.FromMinutes(_options.ChuKyJobPhut);
                _logger.LogInformation("AutoInvoiceFinalizeJob sẽ chạy lại sau: {WaitTime}", timeToWait);
                try
                {
                    await Task.Delay(timeToWait, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }

            _logger.LogInformation("Dịch vụ lập lịch tự chốt hóa đơn tạm tính (AutoInvoiceFinalizeJob) đang dừng.");
        }
    }
}
