namespace QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;

public interface IVnPayPaymentService
{
    // IPN VNPay (spec VNPay §5.2). query: toàn bộ tham số query string. Không ném lỗi nghiệp vụ; luôn trả mã cho VNPay.
    Task<VnPayIpnResult> HandleIpnAsync(IReadOnlyDictionary<string, string> query, CancellationToken ct = default);
}
