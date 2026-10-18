using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Payments;

namespace QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;

// Một biến động số dư báo từ ngân hàng (qua SePay), đã chuẩn hóa: thời gian là UTC, không chứa kiểu ASP.NET.
// Webhook và đồng bộ bù dùng chung DTO này nên dùng chung khóa chống trùng.
public sealed record IncomingBankTransfer(
    // 0 = giao dịch lấy từ API đồng bộ bù (id của API là UUID, không phải số nguyên). Đồng bộ bù luôn có ReferenceCode
    // nên DedupKey không bao giờ dùng SePayId.
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
    // Giao dịch đến từ màn đồng bộ bù: Admin đang xem bảng kết quả nên các kết quả không ghi sổ không bắn cảnh báo Telegram
    // (spec thanh toán trực tuyến §4.3). Kết quả ghi sổ vẫn phát đủ thông báo như webhook.
    public bool LaDongBoBu { get; init; }

    // Giá trị ghi vào LichSuThanhToan.MaGiaoDich. Thiếu mã tham chiếu thì dùng id SePay; đồng bộ bù
    // không gọi ingest cho giao dịch thiếu mã tham chiếu. Vẫn có thể ghi đôi khi bật SePay:SyncRecordEnabled
    // nếu webhook thiếu referenceCode (SEPAY-ID-…) còn API có reference_number cho cùng giao dịch (spec §5.2).
    public string DedupKey => string.IsNullOrWhiteSpace(ReferenceCode)
        ? $"SEPAY-ID-{SePayId}"
        : InvoicePaymentPolicy.SePayLedgerKey(ReferenceCode);
}
