namespace QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;

public interface IPaymentNotificationDispatcher
{
    // Gửi Telegram và email biên lai cho một việc trong hàng đợi (spec thanh toán trực tuyến §6.1 bước 3). Lỗi gửi
    // từng kênh chỉ log Warning và không chặn kênh còn lại.
    Task DispatchAsync(PaymentNotificationWorkItem item, CancellationToken ct = default);
}
