using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using QuanLyChoThuePhongTroWeb.Application.Common.Configurations;
using QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.BackgroundJobs.PaymentNotifications;

// Đọc hàng đợi và gửi Telegram, email biên lai sau khi ghi sổ (spec thanh toán trực tuyến §6.1 bước 3). Mỗi việc chạy
// trong một scope riêng. Lỗi chỉ log Warning kèm hóa đơn và khoản, không thử lại, không làm dừng worker.
// Đăng ký không phụ thuộc BackgroundJobs:Enabled (tắt job định kỳ không được làm mất tin thanh toán).
public sealed class PaymentNotificationWorker(
    ChannelPaymentNotificationQueue queue,
    IServiceScopeFactory scopeFactory,
    TelegramOptions telegram,
    ILogger<PaymentNotificationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!telegram.IsConfigured)
        {
            logger.LogInformation("Telegram chưa bật hoặc thiếu BotToken/DefaultChatId: bỏ qua tin Telegram về thanh toán.");
        }

        try
        {
            await foreach (var item in queue.Reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    using var scope = scopeFactory.CreateScope();
                    var dispatcher = scope.ServiceProvider.GetRequiredService<IPaymentNotificationDispatcher>();
                    await dispatcher.DispatchAsync(item, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Không gửi được thông báo thanh toán: SePay {SePayId}, hóa đơn {HoaDonId}, khoản {LichSuThanhToanId}",
                        item.Outcome.Transfer.SePayId, item.Outcome.HoaDonId, item.Outcome.LichSuThanhToanId);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Host đang dừng.
        }
    }
}
