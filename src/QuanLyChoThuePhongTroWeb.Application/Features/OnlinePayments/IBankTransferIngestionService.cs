namespace QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;

public interface IBankTransferIngestionService
{
    // Không có actorId: chỉ được gọi từ webhook SePay đã xác thực chữ ký, hoặc từ đồng bộ bù đã kiểm Admin
    // (spec thanh toán trực tuyến §9.1). Phân loại rồi giao cho bộ xử lý theo tiền tố; chạy lại bao nhiêu lần
    // cũng không ghi đôi nhờ khóa chống trùng.
    Task<TransferOutcome> IngestAsync(IncomingBankTransfer transfer, CancellationToken ct = default);
}
