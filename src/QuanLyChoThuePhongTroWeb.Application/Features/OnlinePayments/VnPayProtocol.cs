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

    // vnp_ResponseCode và vnp_TransactionStatus của giao dịch thành công.
    public const string SuccessCode = "00";

    private const string ParamPrefix = "vnp_";
    private const string TimeFormat = "yyyyMMddHHmmss";
    private const int VietnamOffsetHours = 7;

    [GeneratedRegex(@"^HD([0-9]{1,9})_([0-9]+)\z", RegexOptions.CultureInvariant)]
    private static partial Regex TxnRefPattern();

    // Chỉ tham số bắt đầu "vnp_" (Ordinal) có giá trị khác rỗng, bỏ vnp_SecureHash và vnp_SecureHashType; sắp theo tên
    // (Ordinal); nối "key=" + UrlEncode(value) bằng "&". Tên tham số không encode.
    // Cách encode chưa xác nhận trên sandbox (spec VNPay §5.3): mọi chỗ encode chỉ nằm trong hàm này.
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
