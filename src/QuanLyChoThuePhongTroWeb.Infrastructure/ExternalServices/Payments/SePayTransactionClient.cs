using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using QuanLyChoThuePhongTroWeb.Application.Common.Configurations;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.ExternalServices.Payments;

// Gọi API v2 GET /transactions của SePay để đồng bộ bù (spec thanh toán trực tuyến §4.3). Header Authorization chứa ApiToken
// nên token, URL, phản hồi và nội dung chuyển khoản không bao giờ được log (không truyền ex vào logger, chỉ log tên kiểu exception
// và mã HTTP); HttpClient đăng ký bằng RemoveAllLoggers để lớp ghi log của HttpClient cũng không in request.
// Một instance dùng cho một lượt đồng bộ (đăng ký transient): giữ khoảng cách giữa hai request để không vượt 3 request/giây.
public sealed class SePayTransactionClient(HttpClient httpClient, SePayOptions options, ILogger<SePayTransactionClient> logger)
    : ISePayTransactionClient
{
    private const int PerPage = 100;
    private const int MaxRetries = 3;
    private const string DateFormat = "yyyy-MM-dd HH:mm:ss";
    private const string InvalidFormatMessage = "Phản hồi SePay không đúng định dạng.";

    private static readonly TimeSpan MinRequestInterval = TimeSpan.FromMilliseconds(350);
    private static readonly TimeSpan DefaultRetryAfter = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan MaxRetryAfter = TimeSpan.FromSeconds(10);

    // Thời điểm (Stopwatch) gửi request trước; null khi chưa gửi request nào.
    private long? _lastRequestTimestamp;

    public async Task<ServiceResult<SePayTransactionPage>> GetIncomingPageAsync(
        DateTime fromUtc, DateTime toUtc, int page, CancellationToken ct = default)
    {
        var token = options.ApiToken?.Trim();
        if (string.IsNullOrEmpty(token))
        {
            return ServiceResult<SePayTransactionPage>.Fail("Chưa cấu hình SePay:ApiToken.");
        }

        var url = options.ApiBaseUrl.Trim().TrimEnd('/') +
                  "/transactions?transaction_date_from=" + Uri.EscapeDataString(ToVietnamText(fromUtc)) +
                  "&transaction_date_to=" + Uri.EscapeDataString(ToVietnamText(toUtc)) +
                  "&transfer_type=in&page=" + page.ToString(CultureInfo.InvariantCulture) +
                  "&per_page=" + PerPage.ToString(CultureInfo.InvariantCulture);

        try
        {
            for (var attempt = 0; ; attempt++)
            {
                await WaitForRateLimitAsync(ct);

                // Mỗi lần gửi một request mới; token gắn vào request, không vào DefaultRequestHeaders của HttpClient dùng chung.
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                using var response = await httpClient.SendAsync(request, ct);

                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    if (attempt >= MaxRetries)
                    {
                        logger.LogWarning("SePay API vẫn trả mã 429 sau {Retries} lần thử lại", MaxRetries);
                        return ServiceResult<SePayTransactionPage>.Fail("SePay đang giới hạn số yêu cầu, thử lại sau ít phút.");
                    }

                    await Task.Delay(RetryDelay(response), ct);
                    continue;
                }

                if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                {
                    var code = (int)response.StatusCode;
                    logger.LogWarning("SePay API từ chối API token (mã {StatusCode})", code);
                    return ServiceResult<SePayTransactionPage>.Fail($"SePay từ chối API token (mã {code}). Kiểm tra SePay:ApiToken.");
                }

                if (!response.IsSuccessStatusCode)
                {
                    var code = (int)response.StatusCode;
                    logger.LogWarning("SePay API trả mã {StatusCode}", code);
                    return ServiceResult<SePayTransactionPage>.Fail($"SePay trả mã {code}.");
                }

                return Parse(await response.Content.ReadAsStringAsync(ct));
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Không truyền ex vào logger: thông báo của HttpRequestException có thể chứa URL.
            logger.LogWarning("Không kết nối được SePay API ({ExceptionType})", ex.GetType().Name);
            return ServiceResult<SePayTransactionPage>.Fail("Không kết nối được SePay. Thử lại sau.");
        }
    }

    // transaction_date_from/to của API là giờ Việt Nam; hệ thống lưu và truyền UTC nên đổi ở đây (ranh giới với API ngoài).
    private static string ToVietnamText(DateTime utc) => utc.AddHours(7).ToString(DateFormat, CultureInfo.InvariantCulture);

    // Thiếu bao nhiêu so với khoảng cách tối thiểu giữa hai request thì chờ bấy nhiêu.
    private async Task WaitForRateLimitAsync(CancellationToken ct)
    {
        if (_lastRequestTimestamp is long last)
        {
            var remaining = MinRequestInterval - Stopwatch.GetElapsedTime(last);
            if (remaining > TimeSpan.Zero)
            {
                await Task.Delay(remaining, ct);
            }
        }

        _lastRequestTimestamp = Stopwatch.GetTimestamp();
    }

    // Retry-After dạng số giây hoặc ngày giờ; không có thì 1 giây; kẹp trong [0, 10 giây].
    private static TimeSpan RetryDelay(HttpResponseMessage response)
    {
        var retryAfter = response.Headers.RetryAfter;
        TimeSpan delay;
        if (retryAfter?.Delta is TimeSpan delta)
        {
            delay = delta;
        }
        else if (retryAfter?.Date is DateTimeOffset date)
        {
            delay = date - DateTimeOffset.UtcNow;
        }
        else
        {
            delay = DefaultRetryAfter;
        }

        return delay < TimeSpan.Zero ? TimeSpan.Zero : delay > MaxRetryAfter ? MaxRetryAfter : delay;
    }

    private ServiceResult<SePayTransactionPage> Parse(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("data", out var data) ||
                data.ValueKind != JsonValueKind.Array)
            {
                return ServiceResult<SePayTransactionPage>.Fail(InvalidFormatMessage);
            }

            var items = new List<SePayApiTransaction>();
            var malformed = 0;
            foreach (var element in data.EnumerateArray())
            {
                if (TryReadTransaction(element, out var transaction))
                {
                    items.Add(transaction);
                }
                else
                {
                    malformed++;
                }
            }

            if (malformed > 0)
            {
                logger.LogWarning("SePay API trả {Count} giao dịch sai định dạng, bỏ qua", malformed);
            }

            return ServiceResult<SePayTransactionPage>.Ok(new SePayTransactionPage(items, ReadHasMore(root), malformed));
        }
        catch (JsonException)
        {
            return ServiceResult<SePayTransactionPage>.Fail(InvalidFormatMessage);
        }
    }

    // Cần đủ id, transaction_date (đúng định dạng) và amount_in; các trường còn lại là chuỗi tùy chọn (null hoặc khác kiểu chuỗi coi như null).
    private static bool TryReadTransaction(JsonElement element, out SePayApiTransaction transaction)
    {
        transaction = null!;
        if (element.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (!TryReadId(element, out var id) ||
            !TryReadDateUtc(element, out var dateUtc) ||
            !TryReadAmount(element, out var amount))
        {
            return false;
        }

        transaction = new SePayApiTransaction(
            id,
            dateUtc,
            OptionalString(element, "account_number"),
            OptionalString(element, "va"),
            OptionalString(element, "transfer_type"),
            amount,
            OptionalString(element, "transaction_content"),
            OptionalString(element, "reference_number"),
            OptionalString(element, "code"),
            OptionalString(element, "bank_brand_name"));
        return true;
    }

    // id của API v2 là chuỗi (UUID); nhận cả số để không vỡ nếu API đổi kiểu.
    private static bool TryReadId(JsonElement element, out string id)
    {
        id = string.Empty;
        if (!element.TryGetProperty("id", out var value))
        {
            return false;
        }

        if (value.ValueKind == JsonValueKind.String)
        {
            var text = value.GetString();
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            id = text;
            return true;
        }

        if (value.ValueKind == JsonValueKind.Number)
        {
            id = value.GetRawText();
            return true;
        }

        return false;
    }

    private static bool TryReadDateUtc(JsonElement element, out DateTime utc)
    {
        utc = default;
        if (!element.TryGetProperty("transaction_date", out var value) || value.ValueKind != JsonValueKind.String ||
            !DateTime.TryParseExact(value.GetString(), DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var local) ||
            local < DateTime.MinValue.AddHours(7))
        {
            return false;
        }

        // transaction_date là giờ Việt Nam (kiểm lại khi thử tay trên Test mode); hệ thống lưu UTC.
        utc = DateTime.SpecifyKind(local.AddHours(-7), DateTimeKind.Utc);
        return true;
    }

    // amount_in có thể là số hoặc chuỗi số ("2000000.00").
    private static bool TryReadAmount(JsonElement element, out decimal amount)
    {
        amount = 0m;
        if (!element.TryGetProperty("amount_in", out var value))
        {
            return false;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number => value.TryGetDecimal(out amount),
            JsonValueKind.String => decimal.TryParse(value.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out amount),
            _ => false
        };
    }

    private static string? OptionalString(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }

    // meta.pagination.has_more; thiếu thì so current_page với last_page; cả hai đều thiếu thì coi là hết trang.
    private static bool ReadHasMore(JsonElement root)
    {
        if (!root.TryGetProperty("meta", out var meta) || meta.ValueKind != JsonValueKind.Object ||
            !meta.TryGetProperty("pagination", out var pagination) || pagination.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (pagination.TryGetProperty("has_more", out var hasMore) &&
            hasMore.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            return hasMore.GetBoolean();
        }

        return TryReadInt(pagination, "current_page", out var current) &&
               TryReadInt(pagination, "last_page", out var last) &&
               current < last;
    }

    private static bool TryReadInt(JsonElement element, string name, out int value)
    {
        value = 0;
        return element.TryGetProperty(name, out var property) &&
               property.ValueKind == JsonValueKind.Number &&
               property.TryGetInt32(out value);
    }
}
