using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests.Fakes
{
    // Client API SePay giả: trả lần lượt các kết quả đã xếp hàng (mỗi lần gọi lấy một kết quả, không phụ thuộc số trang),
    // hết hàng thì dùng Fallback; ghi lại (fromUtc, toUtc, page) của từng lần gọi.
    public class FakeSePayTransactionClient : ISePayTransactionClient
    {
        public Queue<ServiceResult<SePayTransactionPage>> Results { get; } = new();
        public Func<int, ServiceResult<SePayTransactionPage>>? Fallback { get; set; }
        public List<(DateTime FromUtc, DateTime ToUtc, int Page)> Calls { get; } = new();

        public Task<ServiceResult<SePayTransactionPage>> GetIncomingPageAsync(DateTime fromUtc, DateTime toUtc, int page, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            Calls.Add((fromUtc, toUtc, page));

            if (Results.Count > 0)
            {
                return Task.FromResult(Results.Dequeue());
            }

            if (Fallback != null)
            {
                return Task.FromResult(Fallback(page));
            }

            throw new InvalidOperationException($"Chưa xếp kết quả cho lần gọi thứ {Calls.Count} (trang {page}).");
        }

        public void Enqueue(IReadOnlyList<SePayApiTransaction> items, bool hasMore, int malformed = 0)
            => Results.Enqueue(ServiceResult<SePayTransactionPage>.Ok(new SePayTransactionPage(items, hasMore, malformed)));

        public void EnqueueFailure(string message)
            => Results.Enqueue(ServiceResult<SePayTransactionPage>.Fail(message));
    }

    // Điểm vào ghi sổ giả: ghi lại giao dịch nhận được và trả outcome cấu hình sẵn (mặc định Recorded).
    public class RecordingBankTransferIngestion : IBankTransferIngestionService
    {
        public List<IncomingBankTransfer> Transfers { get; } = new();
        public Func<IncomingBankTransfer, TransferOutcome>? Responder { get; set; }

        public Task<TransferOutcome> IngestAsync(IncomingBankTransfer transfer, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            Transfers.Add(transfer);
            return Task.FromResult(Responder != null
                ? Responder(transfer)
                : new TransferOutcome { Kind = TransferOutcomeKind.Recorded, Transfer = transfer, HoaDonId = 7, MaHoaDon = "HD-7" });
        }
    }
}
