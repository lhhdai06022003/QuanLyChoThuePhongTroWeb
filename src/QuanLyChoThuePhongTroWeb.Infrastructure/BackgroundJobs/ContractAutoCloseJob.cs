using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using QuanLyChoThuePhongTroWeb.Application.Features.HopDongs.UseCases;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.BackgroundJobs
{
    public class ContractAutoCloseJob : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<ContractAutoCloseJob> _logger;

        public ContractAutoCloseJob(
            IServiceScopeFactory scopeFactory,
            ILogger<ContractAutoCloseJob> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Dịch vụ lập lịch tự động đóng hợp đồng hết hạn (ContractAutoCloseJob) đã khởi động.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var useCase = scope.ServiceProvider.GetRequiredService<IContractAutoCloseUseCase>();
                    var closedCount = await useCase.ExecuteAsync(stoppingToken);
                    if (closedCount > 0)
                    {
                        _logger.LogInformation("ContractAutoCloseJob đã đóng {Count} hợp đồng hết hạn thành công.", closedCount);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi xảy ra trong quá trình tự động đóng hợp đồng của ContractAutoCloseJob.");
                }

                if (stoppingToken.IsCancellationRequested)
                {
                    break;
                }

                var now = DateTime.UtcNow;
                var vnNow = now.AddHours(7);
                var vnNextMidnight = vnNow.Date.AddDays(1);
                var timeToWait = vnNextMidnight - vnNow;
                if (timeToWait <= TimeSpan.Zero)
                {
                    timeToWait = TimeSpan.FromDays(1);
                }

                _logger.LogInformation("ContractAutoCloseJob sẽ chạy lại sau: {WaitTime}", timeToWait);
                try
                {
                    await Task.Delay(timeToWait, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }

            _logger.LogInformation("Dịch vụ lập lịch tự động đóng hợp đồng hết hạn (ContractAutoCloseJob) đang dừng.");
        }
    }
}
