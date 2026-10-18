using System.Globalization;
using QuanLyChoThuePhongTroWeb.Application.Common.Configurations;

namespace QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;

// Deeplink mở app ngân hàng với tài khoản, số tiền và nội dung điền sẵn (spec thanh toán trực tuyến §3.2 mục 3).
// Thuần: không DI, không đọc cấu hình. appId theo danh sách app của VietQR (api.vietqr.io/v2/android-app-deeplinks và ios-app-deeplinks).
public static class VietQrDeeplinks
{
    private const string BaseUrl = "https://dl.vietqr.io/pay";

    // Thứ tự hiển thị trên lưới: MBBank, Vietcombank, Techcombank, BIDV, ACB, VietinBank, VPBank, TPBank, Agribank, OCB, HDBank, VIB.
    public static IReadOnlyList<(string TenNganHang, string AppId)> Banks { get; } = new (string TenNganHang, string AppId)[]
    {
        ("MBBank", "mb"),
        ("Vietcombank", "vcb"),
        ("Techcombank", "tcb"),
        ("BIDV", "bidv"),
        ("ACB", "acb"),
        ("VietinBank", "icb"),
        ("VPBank", "vpb"),
        ("TPBank", "tpb"),
        ("Agribank", "vba"),
        ("OCB", "ocb"),
        ("HDBank", "hdb"),
        ("VIB", "vib")
    };

    // https://dl.vietqr.io/pay?app={appId}&ba={accountNumber}@{bankId}&am={amount}&tn={memo}&bn={accountName}
    // Mỗi giá trị được mã hóa riêng; riêng ba thì mã hóa từng phần để dấu @ giữ nguyên.
    public static string Build(string appId, string accountNumber, string bankId, decimal amount, string memo, string accountName)
    {
        var ba = Uri.EscapeDataString(accountNumber) + "@" + Uri.EscapeDataString(bankId);
        var am = amount.ToString("0", CultureInfo.InvariantCulture);

        return $"{BaseUrl}?app={Uri.EscapeDataString(appId)}&ba={ba}&am={am}&tn={Uri.EscapeDataString(memo)}&bn={Uri.EscapeDataString(accountName)}";
    }

    public static IReadOnlyList<BankDeeplinkDto> BuildAll(VietQrSettings settings, decimal amount, string memo)
    {
        return Banks
            .Select(bank => new BankDeeplinkDto(
                bank.TenNganHang,
                Build(bank.AppId, settings.AccountNumber, settings.BankId, amount, memo, settings.AccountName)))
            .ToList();
    }
}
