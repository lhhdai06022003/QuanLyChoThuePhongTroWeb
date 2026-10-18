namespace QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;

public interface IPaymentOutcomePublisher
{
    // Việc sau khi có kết quả xử lý giao dịch (spec thanh toán trực tuyến §6.1 bước 2-3): gửi sự kiện SignalR cho
    // panel của khách và đưa việc gửi Telegram, email biên lai vào hàng đợi. Không làm đổi kết quả đã ghi sổ:
    // lỗi chỉ được log.
    Task PublishAsync(TransferOutcome outcome, CancellationToken ct = default);
}
