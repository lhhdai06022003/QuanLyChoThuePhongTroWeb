namespace QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;

// Gửi tin Telegram (spec thanh toán trực tuyến §6.2). Infrastructure cài bằng HttpClient gọi Bot API.
public interface ITelegramNotifier
{
    // Gửi một tin HTML (người gọi đã escape) tới một chat. Không ném lỗi (trừ khi bị hủy bằng ct): thất bại trả
    // IsSuccess = false, ErrorMessage không chứa token hay nội dung tin.
    Task<(bool IsSuccess, string ErrorMessage)> SendMessageAsync(string chatId, string htmlText, CancellationToken ct = default);
}
