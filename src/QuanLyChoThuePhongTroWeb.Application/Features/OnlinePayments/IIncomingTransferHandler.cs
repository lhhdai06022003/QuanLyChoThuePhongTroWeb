namespace QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;

// Bộ xử lý giao dịch tiền vào theo tiền tố của mã chuyển khoản. Đợt 1 chỉ có bộ xử lý hóa đơn (TTPTHD);
// thanh toán giữ chỗ thêm bộ xử lý và tiền tố riêng, không sửa phần ghi sổ hóa đơn.
public interface IIncomingTransferHandler
{
    // Viết hoa, ví dụ "TTPTHD".
    string Prefix { get; }

    // code là mã đã chuẩn hóa, bắt đầu bằng Prefix.
    Task<TransferOutcome> HandleAsync(IncomingBankTransfer transfer, string code, CancellationToken ct = default);
}
