using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;

// Chữ ký, mã đơn và giờ của VNPay (spec VNPay §5.3, §5.4). Thuần: không DI, không log, không đọc DB.
// Giờ VNPay là giờ Việt Nam cố định UTC+7, giống PaymentNotificationFormatter.
public static partial class VnPayProtocol
{
    public const string TmnCodeKey = "vnp_TmnCode";
    public const string AmountKey = "vnp_Amount";
    public const string TxnRefKey = "vnp_TxnRef";
    public const string ResponseCodeKey = "vnp_ResponseCode";
    public const string TransactionStatusKey = "vnp_TransactionStatus";
    public const string TransactionNoKey = "vnp_TransactionNo";
    public const string BankCodeKey = "vnp_BankCode";
    public const string CardTypeKey = "vnp_CardType";
    public const string PayDateKey = "vnp_PayDate";
    public const string OrderInfoKey = "vnp_OrderInfo";
    public const string SecureHashKey = "vnp_SecureHash";
    public const string SecureHashTypeKey = "vnp_SecureHashType";

    // Tham số và giá trị cố định khi tạo link thanh toán (spec VNPay §5.1).
    public const string VersionKey = "vnp_Version";
    public const string CommandKey = "vnp_Command";
    public const string CurrCodeKey = "vnp_CurrCode";
    public const string OrderTypeKey = "vnp_OrderType";
    public const string LocaleKey = "vnp_Locale";
    public const string ReturnUrlKey = "vnp_ReturnUrl";
    public const string IpAddrKey = "vnp_IpAddr";
    public const string CreateDateKey = "vnp_CreateDate";
    public const string ExpireDateKey = "vnp_ExpireDate";

    public const string Version = "2.1.0";
    public const string PayCommand = "pay";
    public const string CurrCode = "VND";
    public const string OrderType = "other";
    public const string Locale = "vn";

    // vnp_ResponseCode và vnp_TransactionStatus của giao dịch thành công.
    public const string SuccessCode = "00";

    // vnp_ResponseCode khi khách tự hủy giao dịch trên trang VNPay.
    public const string CancelledCode = "24";

    // Độ dài tối đa của vnp_OrderInfo (VNPay).
    public const int MaxOrderInfoLength = 255;

    private const string ParamPrefix = "vnp_";
    private const string TimeFormat = "yyyyMMddHHmmss";
    private const int VietnamOffsetHours = 7;

    [GeneratedRegex(@"^HD([0-9]{1,9})_([0-9]+)\z", RegexOptions.CultureInvariant)]
    private static partial Regex TxnRefPattern();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex SpacesPattern();

    // Chỉ tham số bắt đầu "vnp_" (Ordinal) có giá trị khác rỗng, bỏ vnp_SecureHash và vnp_SecureHashType; sắp theo tên
    // (Ordinal); nối "key=" + UrlEncode(value) bằng "&". Tên tham số không encode.
    // Cách encode đã xác nhận trên sandbox 26/10/2026 (spec VNPay §5.3); mọi chỗ encode chỉ nằm trong hàm này.
    public static string BuildSignData(IReadOnlyDictionary<string, string> parameters)
    {
        return string.Join("&", parameters
            .Where(p => p.Key.StartsWith(ParamPrefix, StringComparison.Ordinal)
                && !string.IsNullOrEmpty(p.Value)
                && p.Key != SecureHashKey
                && p.Key != SecureHashTypeKey)
            .OrderBy(p => p.Key, StringComparer.Ordinal)
            .Select(p => p.Key + "=" + WebUtility.UrlEncode(p.Value)));
    }

    // HMAC-SHA512, khóa và dữ liệu UTF-8, kết quả hex chữ thường (128 ký tự).
    public static string Sign(string data, string hashSecret)
    {
        var hash = HMACSHA512.HashData(Encoding.UTF8.GetBytes(hashSecret), Encoding.UTF8.GetBytes(data));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    // false khi hashSecret rỗng hoặc thiếu vnp_SecureHash. So bằng thời gian hằng, không phân biệt hoa thường.
    public static bool Verify(IReadOnlyDictionary<string, string> parameters, string hashSecret)
    {
        if (string.IsNullOrWhiteSpace(hashSecret)
            || !parameters.TryGetValue(SecureHashKey, out var received)
            || string.IsNullOrWhiteSpace(received))
        {
            return false;
        }

        var expected = Sign(BuildSignData(parameters), hashSecret);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(expected),
            Encoding.ASCII.GetBytes(received.Trim().ToLowerInvariant()));
    }

    // URL thanh toán: paymentUrl + "?" + chuỗi ký + "&vnp_SecureHash=" + chữ ký. Chuỗi ký chính là query (mẫu .NET của VNPay),
    // nên mọi chỗ encode vẫn chỉ nằm trong BuildSignData.
    public static string BuildPaymentUrl(string paymentUrl, IReadOnlyDictionary<string, string> parameters, string hashSecret)
    {
        var signData = BuildSignData(parameters);
        return paymentUrl + "?" + signData + "&" + SecureHashKey + "=" + Sign(signData, hashSecret);
    }

    // vnp_OrderInfo: "Thanh toan hoa don " + mã hóa đơn đã bỏ dấu tiếng Việt, chỉ giữ chữ cái, chữ số, '-', '_' và dấu cách
    // (VNPay yêu cầu tiếng Việt không dấu, không ký tự đặc biệt). Gộp khoảng trắng liên tiếp, cắt MaxOrderInfoLength.
    public static string BuildOrderInfo(string? maHoaDon)
    {
        const string prefix = "Thanh toan hoa don";

        var builder = new StringBuilder();
        foreach (var c in (maHoaDon ?? string.Empty).Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            var ch = c switch { 'đ' => 'd', 'Đ' => 'D', _ => c };
            if (ch is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-' or '_')
            {
                builder.Append(ch);
            }
            else if (char.IsWhiteSpace(ch))
            {
                builder.Append(' ');
            }
        }

        var code = SpacesPattern().Replace(builder.ToString(), " ").Trim();
        var info = code.Length == 0 ? prefix : prefix + " " + code;
        return info.Length > MaxOrderInfoLength ? info[..MaxOrderInfoLength].TrimEnd() : info;
    }

    // vnp_TxnRef: HD{HoaDonId}_{Ticks}. Mỗi lần tạo link có một mã khác nhau (VNPay không cho trùng mã trong ngày).
    public static string BuildTxnRef(int hoaDonId, DateTime utcNow)
    {
        return string.Create(CultureInfo.InvariantCulture, $"HD{hoaDonId}_{utcNow.Ticks}");
    }

    // HoaDonId là 1-9 chữ số ASCII và là số nguyên dương; phân biệt hoa thường.
    public static bool TryParseTxnRef(string? txnRef, out int hoaDonId)
    {
        hoaDonId = 0;
        if (string.IsNullOrEmpty(txnRef))
        {
            return false;
        }

        var match = TxnRefPattern().Match(txnRef);
        return match.Success
            && int.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out hoaDonId)
            && hoaDonId > 0;
    }

    public static string FormatVietnamTime(DateTime utc)
    {
        return utc.AddHours(VietnamOffsetHours).ToString(TimeFormat, CultureInfo.InvariantCulture);
    }

    // Giờ Việt Nam yyyyMMddHHmmss sang UTC (Kind = Utc).
    public static bool TryParseVietnamTime(string? value, out DateTime utc)
    {
        utc = default;
        if (!DateTime.TryParseExact(value, TimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var vietnam))
        {
            return false;
        }

        utc = DateTime.SpecifyKind(vietnam.AddHours(-VietnamOffsetHours), DateTimeKind.Utc);
        return true;
    }
}
