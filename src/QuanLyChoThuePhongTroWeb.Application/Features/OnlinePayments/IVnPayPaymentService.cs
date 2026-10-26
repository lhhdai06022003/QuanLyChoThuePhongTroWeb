using QuanLyChoThuePhongTroWeb.Application.Common.Models;

namespace QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;

public interface IVnPayPaymentService
{
    // IPN VNPay (spec VNPay §5.2). query: toàn bộ tham số query string. Không ném lỗi nghiệp vụ; luôn trả mã cho VNPay.
    Task<VnPayIpnResult> HandleIpnAsync(IReadOnlyDictionary<string, string> query, CancellationToken ct = default);

    // Tạo link thanh toán với số tiền bằng số còn lại (spec VNPay §4.2, §5.1). Không ghi DB.
    // Hóa đơn không thuộc khách: NotFound; hóa đơn không nhận tiền được: Fail kèm lý do tiếng Việt.
    Task<ServiceResult<string>> CreatePaymentUrlAsync(int hoaDonId, int nguoiThueId, string clientIp, string returnUrl, CancellationToken ct = default);

    // Dữ liệu trang kết quả (spec VNPay §4.3). Chỉ đọc, không ghi sổ, không ném lỗi nghiệp vụ.
    Task<VnPayReturnResult> GetReturnResultAsync(IReadOnlyDictionary<string, string> query, int nguoiThueId, CancellationToken ct = default);
}
