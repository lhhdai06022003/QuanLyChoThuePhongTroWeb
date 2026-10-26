namespace QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;

public interface IReservationVnPayService
{
    // IPN VNPay cho tiền giữ chỗ (spec giữ chỗ §6.2; vnp_TxnRef = GC{YeuCauGiuChoId}_{Ticks}).
    // query: toàn bộ tham số query string. Không ném lỗi nghiệp vụ; luôn trả mã cho VNPay.
    Task<VnPayIpnResult> HandleIpnAsync(IReadOnlyDictionary<string, string> query, CancellationToken ct = default);
}
