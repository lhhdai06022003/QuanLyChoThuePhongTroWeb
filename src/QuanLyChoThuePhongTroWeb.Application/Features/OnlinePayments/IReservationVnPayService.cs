using QuanLyChoThuePhongTroWeb.Application.Common.Models;

namespace QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;

public interface IReservationVnPayService
{
    // IPN VNPay cho tiền giữ chỗ (spec giữ chỗ §6.2; vnp_TxnRef = GC{YeuCauGiuChoId}_{Ticks}).
    // query: toàn bộ tham số query string. Không ném lỗi nghiệp vụ; luôn trả mã cho VNPay.
    Task<VnPayIpnResult> HandleIpnAsync(IReadOnlyDictionary<string, string> query, CancellationToken ct = default);

    // Tạo link VNPay cho giữ chỗ (spec giữ chỗ §5.2). Không ghi DB.
    Task<ServiceResult<string>> CreatePaymentUrlAsync(int actorId, int reservationId, string clientIp, string returnUrl, CancellationToken ct = default);

    // Dữ liệu trang kết quả VNPay cho giữ chỗ (spec giữ chỗ §5.3). Chỉ đọc, không ghi DB.
    Task<ReservationVnPayReturnResult> GetReturnResultAsync(IReadOnlyDictionary<string, string> query, int actorId, CancellationToken ct = default);
}
