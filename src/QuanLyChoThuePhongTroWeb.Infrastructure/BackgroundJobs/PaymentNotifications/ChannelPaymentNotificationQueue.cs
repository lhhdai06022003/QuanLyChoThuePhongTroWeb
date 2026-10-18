using System.Threading.Channels;
using QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.BackgroundJobs.PaymentNotifications;

// Hàng đợi trong process cho việc gửi Telegram và email biên lai (spec thanh toán trực tuyến §6.1 bước 3).
// Một worker đọc ra (PaymentNotificationWorker). Đầy thì từ chối ngay thay vì chặn webhook.
public sealed class ChannelPaymentNotificationQueue : IPaymentNotificationQueue
{
    private const int Capacity = 1000;

    private readonly Channel<PaymentNotificationWorkItem> _channel = Channel.CreateBounded<PaymentNotificationWorkItem>(
        new BoundedChannelOptions(Capacity)
        {
            SingleReader = true,
            FullMode = BoundedChannelFullMode.Wait
        });

    public ChannelReader<PaymentNotificationWorkItem> Reader => _channel.Reader;

    public ValueTask<bool> EnqueueAsync(PaymentNotificationWorkItem item, CancellationToken ct = default)
    {
        return new ValueTask<bool>(_channel.Writer.TryWrite(item));
    }
}
