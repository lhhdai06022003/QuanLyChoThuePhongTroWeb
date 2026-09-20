using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using QuanLyChoThuePhongTroWeb.Application.Features.HopDongs.UseCases;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.BackgroundJobs
{
    public class ContractExpiryAlertJob : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<ContractExpiryAlertJob> _logger;

        public ContractExpiryAlertJob(
            IServiceScopeFactory scopeFactory,
            ILogger<ContractExpiryAlertJob> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Dịch vụ lập lịch tự động cảnh báo hợp đồng sắp hết hạn (ContractExpiryAlertJob) đã khởi động.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var useCase = scope.ServiceProvider.GetRequiredService<IContractExpiryAlertUseCase>();
                    var sentCount = await useCase.ExecuteAsync(stoppingToken);
                    if (sentCount > 0)
                    {
                        _logger.LogInformation("ContractExpiryAlertJob đã gửi thành công {Count} cảnh báo hợp đồng sắp hết hạn.", sentCount);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi xảy ra trong quá trình quét và gửi cảnh báo hợp đồng sắp hết hạn của ContractExpiryAlertJob.");
                }

                if (stoppingToken.IsCancellationRequested)
                {
                    break;
                }

                var now = DateTime.UtcNow;
                var vnNow = now.AddHours(7);
                var vnNextEightAM = vnNow.Date.AddHours(8);
                if (vnNow >= vnNextEightAM)
                {
                    vnNextEightAM = vnNextEightAM.AddDays(1);
                }
                var timeToWait = vnNextEightAM - vnNow;
                if (timeToWait <= TimeSpan.Zero)
                {
                    timeToWait = TimeSpan.FromDays(1);
                }

                _logger.LogInformation("ContractExpiryAlertJob sẽ chạy lại sau: {WaitTime}", timeToWait);
                try
                {
                    await Task.Delay(timeToWait, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }

            _logger.LogInformation("Dịch vụ lập lịch tự động cảnh báo hợp đồng sắp hết hạn (ContractExpiryAlertJob) đang dừng.");
        }
    }
}
