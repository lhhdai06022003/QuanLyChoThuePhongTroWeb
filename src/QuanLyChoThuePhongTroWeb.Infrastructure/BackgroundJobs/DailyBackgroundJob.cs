using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.BackgroundJobs
{
    // Khung chung cho job chạy mỗi ngày một lượt theo DailyJobSchedule. Lớp con chỉ gọi use case trong một scope.
    public abstract class DailyBackgroundJob : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger _logger;

        protected DailyBackgroundJob(IServiceScopeFactory scopeFactory, TimeProvider timeProvider, ILogger logger)
        {
            _scopeFactory = scopeFactory;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        // Tên hiện trong log, ví dụ "InvoiceReminderJob (nhắc hóa đơn quá hạn)".
        protected abstract string TenJob { get; }

        // Một lượt quét và gửi; trả số thông báo đã gửi. Ném lỗi chỉ khi chưa gửi gì (lượt đó được thử lại).
        protected abstract Task<int> RunOnceAsync(IServiceProvider services, CancellationToken cancellationToken);

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Dịch vụ {TenJob} đã khởi động.", TenJob);

            try
            {
                DateTimeOffset? lanTruoc = null;
                while (!stoppingToken.IsCancellationRequested)
                {
                    var now = _timeProvider.GetUtcNow();
                    var lanChay = DailyJobSchedule.NextRunUtc(now, lanTruoc);
                    _logger.LogInformation("{TenJob} sẽ chạy lại lúc {LanChayUtc:yyyy-MM-dd HH:mm} UTC (sau {WaitTime}).",
                        TenJob, lanChay, DailyJobSchedule.DelayUntil(lanChay, now));

                    await Task.Delay(DailyJobSchedule.DelayUntil(lanChay, now), _timeProvider, stoppingToken);
                    lanTruoc = lanChay;

                    await RunWithRetryAsync(lanChay, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Web đang dừng.
            }

            _logger.LogInformation("Dịch vụ {TenJob} đang dừng.", TenJob);
        }

        private async Task RunWithRetryAsync(DateTimeOffset lanChay, CancellationToken stoppingToken)
        {
            while (true)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var sentCount = await RunOnceAsync(scope.ServiceProvider, stoppingToken);
                    if (sentCount > 0)
                    {
                        _logger.LogInformation("{TenJob} đã gửi thành công {Count} thông báo.", TenJob, sentCount);
                    }
                    return;
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    var retry = DailyJobSchedule.RetryDelay(lanChay, _timeProvider.GetUtcNow());
                    if (retry is null)
                    {
                        _logger.LogError(ex, "{TenJob} lỗi và đã hết thời gian thử lại, bỏ lượt hôm nay.", TenJob);
                        return;
                    }

                    _logger.LogError(ex, "{TenJob} lỗi, thử lại sau {RetryDelay}.", TenJob, retry.Value);
                    await Task.Delay(retry.Value, _timeProvider, stoppingToken);
                }
            }
        }
    }
}
