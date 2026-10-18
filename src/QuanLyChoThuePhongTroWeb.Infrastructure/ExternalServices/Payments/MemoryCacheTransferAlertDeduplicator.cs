using Microsoft.Extensions.Caching.Memory;
using QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.ExternalServices.Payments;

// Nhớ khóa chống trùng của các cảnh báo trong IMemoryCache 24 giờ (spec thanh toán trực tuyến §5.3).
// Mất khi khởi động lại (chấp nhận); đăng ký singleton để mọi request dùng chung một bộ nhớ.
public sealed class MemoryCacheTransferAlertDeduplicator(IMemoryCache cache) : ITransferAlertDeduplicator
{
    private static readonly TimeSpan Window = TimeSpan.FromHours(24);

    private readonly object _lock = new();

    public bool TryRegister(string dedupKey)
    {
        var key = "sepay-alert:" + dedupKey;
        lock (_lock)
        {
            if (cache.TryGetValue(key, out _))
            {
                return false;
            }

            cache.Set(key, true, Window);
            return true;
        }
    }
}
