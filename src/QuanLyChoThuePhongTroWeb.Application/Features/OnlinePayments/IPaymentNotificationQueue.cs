namespace QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;

// Hàng đợi trong process cho việc gửi Telegram và email biên lai sau khi ghi sổ (spec thanh toán trực tuyến §6.1 bước 3).
// Infrastructure cài bằng Channel và một worker nền đọc ra; khởi động lại khi còn tin chưa gửi thì mất tin (spec §14).
public interface IPaymentNotificationQueue
{
    // False khi hàng đợi không nhận được việc (đầy).
    ValueTask<bool> EnqueueAsync(PaymentNotificationWorkItem item, CancellationToken ct = default);
}
