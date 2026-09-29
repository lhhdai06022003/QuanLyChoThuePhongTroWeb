using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using QuanLyChoThuePhongTroWeb.Application.Features.GiuChos.UseCases;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.BackgroundJobs;

public sealed class HetHanGiuChoJob(IServiceScopeFactory scopes, ILogger<HetHanGiuChoJob> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var useCase = scope.ServiceProvider.GetRequiredService<IHetHanGiuChoUseCase>();
                await useCase.ExecuteAsync(DateTime.UtcNow);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Không thể xử lý yêu cầu giữ chỗ hết hạn."); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
