namespace QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;

// Chống bắn cảnh báo Telegram lặp khi SePay gửi lại hoặc replay cùng một giao dịch (spec thanh toán trực tuyến §5.3).
// Trạng thái nằm trong bộ nhớ, mất khi khởi động lại (chấp nhận).
public interface ITransferAlertDeduplicator
{
    // True nếu dedupKey chưa gặp trong 24 giờ qua; gọi này đánh dấu luôn là đã gặp.
    bool TryRegister(string dedupKey);
}
