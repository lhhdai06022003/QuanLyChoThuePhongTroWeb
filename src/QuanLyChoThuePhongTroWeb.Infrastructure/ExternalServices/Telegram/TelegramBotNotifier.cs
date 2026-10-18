using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using QuanLyChoThuePhongTroWeb.Application.Common.Configurations;
using QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.ExternalServices.Telegram;

// Gửi tin qua Telegram Bot API (spec thanh toán trực tuyến §6.2). URL gọi chứa BotToken nên token, URL và nội dung tin
// không bao giờ được log; HttpClient đăng ký bằng RemoveAllLoggers để lớp ghi log của HttpClient cũng không in URL.
public sealed class TelegramBotNotifier(
    HttpClient httpClient,
    TelegramOptions options,
    ILogger<TelegramBotNotifier> logger) : ITelegramNotifier
{
    private const string ApiBase = "https://api.telegram.org";
    private const string NotConfiguredMessage = "Telegram chưa được cấu hình.";

    public async Task<(bool IsSuccess, string ErrorMessage)> SendMessageAsync(string chatId, string htmlText, CancellationToken ct = default)
    {
        if (!options.IsConfigured || string.IsNullOrWhiteSpace(chatId))
        {
            return (false, NotConfiguredMessage);
        }

        try
        {
            var url = $"{ApiBase}/bot{options.BotToken.Trim()}/sendMessage";
            using var response = await httpClient.PostAsync(
                url,
                JsonContent.Create(new { chat_id = chatId, text = htmlText, parse_mode = "HTML" }),
                ct);

            if (response.IsSuccessStatusCode)
            {
                return (true, string.Empty);
            }

            logger.LogWarning("Telegram trả mã {StatusCode} khi gửi tin", (int)response.StatusCode);
            return (false, $"Telegram trả mã {(int)response.StatusCode}");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Không truyền ex vào logger: thông báo của HttpRequestException có thể chứa URL có token.
            logger.LogWarning("Không kết nối được Telegram ({ExceptionType})", ex.GetType().Name);
            return (false, "Không kết nối được Telegram.");
        }
    }
}
