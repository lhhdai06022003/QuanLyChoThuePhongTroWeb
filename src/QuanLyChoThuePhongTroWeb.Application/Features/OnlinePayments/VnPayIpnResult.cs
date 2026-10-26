namespace QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;

// Mã và thông điệp trả cho VNPay sau IPN. Theo tài liệu VNPay, không phải giao diện nên giữ tiếng Anh.
public sealed record VnPayIpnResult(string RspCode, string Message)
{
    public static readonly VnPayIpnResult Confirmed = new("00", "Confirm Success");
    public static readonly VnPayIpnResult OrderNotFound = new("01", "Order not found");
    public static readonly VnPayIpnResult AlreadyConfirmed = new("02", "Order already confirmed");
    public static readonly VnPayIpnResult InvalidAmount = new("04", "Invalid amount");
    public static readonly VnPayIpnResult InvalidSignature = new("97", "Invalid signature");
    public static readonly VnPayIpnResult UnknownError = new("99", "Unknown error");
}
