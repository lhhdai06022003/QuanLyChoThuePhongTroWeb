using QuanLyChoThuePhongTroWeb.Application.Common.Models;

namespace QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;

// Một giao dịch từ API v2 GET /transactions của SePay (spec thanh toán trực tuyến §4.3), đã đổi transaction_date (giờ Việt Nam) sang UTC.
public sealed record SePayApiTransaction(
    // id (UUID) của API, khác id số nguyên của webhook nên không dùng để chống trùng với webhook.
    string Id,
    DateTime TransactionDateUtc,
    // account_number
    string? AccountNumber,
    // va
    string? SubAccount,
    // transfer_type
    string? TransferType,
    // amount_in
    decimal AmountIn,
    // transaction_content
    string? Content,
    // reference_number, có thể null
    string? ReferenceNumber,
    // code
    string? Code,
    // bank_brand_name
    string? BankBrandName);

// Một trang kết quả. SoGiaoDichSaiDinhDang là số phần tử của trang bị bỏ vì thiếu hoặc sai id, transaction_date, amount_in.
public sealed record SePayTransactionPage(IReadOnlyList<SePayApiTransaction> Items, bool HasMore, int SoGiaoDichSaiDinhDang);

// Trừu tượng gọi API v2 của SePay (Infrastructure cài). Thuần dữ liệu: không có kiểu HTTP.
public interface ISePayTransactionClient
{
    // Một trang tiền vào (transfer_type=in, 100 giao dịch mỗi trang) trong [fromUtc, toUtc]. Tự chờ và thử lại khi gặp 429.
    // Lỗi mạng, lỗi xác thực hay phản hồi sai định dạng trả Fail với thông báo tiếng Việt, không chứa token.
    Task<ServiceResult<SePayTransactionPage>> GetIncomingPageAsync(DateTime fromUtc, DateTime toUtc, int page, CancellationToken ct = default);
}
