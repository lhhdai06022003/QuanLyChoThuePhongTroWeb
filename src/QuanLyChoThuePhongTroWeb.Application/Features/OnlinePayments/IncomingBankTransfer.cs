using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Payments;

namespace QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;

// Một biến động số dư báo từ ngân hàng (qua SePay), đã chuẩn hóa: thời gian là UTC, không chứa kiểu ASP.NET.
// Webhook và đồng bộ bù dùng chung DTO này nên dùng chung khóa chống trùng.
public sealed record IncomingBankTransfer(
    long SePayId,
    string? Gateway,
    DateTime TransactionDateUtc,
    string? AccountNumber,
    string? SubAccount,
    string? Code,
    string? Content,
    string? TransferType,
    decimal TransferAmount,
    string? ReferenceCode,
    string? Description)
{
    // Giá trị ghi vào LichSuThanhToan.MaGiaoDich. Thiếu mã tham chiếu thì dùng id SePay; đồng bộ bù
    // không gọi ingest cho giao dịch thiếu mã tham chiếu nên hai đường không thể ghi đôi.
    public string DedupKey => string.IsNullOrWhiteSpace(ReferenceCode)
        ? $"SEPAY-ID-{SePayId}"
        : InvoicePaymentPolicy.SePayLedgerKey(ReferenceCode);
}
