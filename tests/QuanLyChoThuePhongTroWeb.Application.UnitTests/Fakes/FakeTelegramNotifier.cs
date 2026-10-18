using QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests.Fakes
{
    // Ghi lại mọi lần gửi (kể cả lần cấu hình cho lỗi). Không gọi mạng.
    public class FakeTelegramNotifier : ITelegramNotifier
    {
        public List<(string ChatId, string Text)> Messages { get; } = new();
        // Chat trong tập này trả IsSuccess = false.
        public HashSet<string> FailChatIds { get; } = new();
        // Chat trong tập này ném exception.
        public HashSet<string> ThrowChatIds { get; } = new();

        public Task<(bool IsSuccess, string ErrorMessage)> SendMessageAsync(string chatId, string htmlText, CancellationToken ct = default)
        {
            Messages.Add((chatId, htmlText));

            if (ThrowChatIds.Contains(chatId))
            {
                throw new InvalidOperationException("Telegram down");
            }

            return Task.FromResult(FailChatIds.Contains(chatId)
                ? (false, "Telegram trả mã 500")
                : (true, string.Empty));
        }
    }
}
