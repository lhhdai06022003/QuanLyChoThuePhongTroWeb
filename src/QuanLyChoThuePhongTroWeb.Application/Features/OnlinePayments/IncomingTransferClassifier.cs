using QuanLyChoThuePhongTroWeb.Application.Common.Configurations;

namespace QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;

public enum TransferClassificationKind
{
    Ignored,
    WrongAccount,
    Unmatched,
    Matched
}

// Handler chỉ có khi Matched. Code có khi tách được mã, kể cả khi mã không thuộc tiền tố nào (Unmatched).
public sealed record TransferClassification(TransferClassificationKind Kind, IIncomingTransferHandler? Handler, string? Code);

// Phân loại giao dịch tiền vào (spec thanh toán trực tuyến §4.2). Thuần: không đọc DB, không log.
public sealed class IncomingTransferClassifier
{
    private const string IncomingType = "in";

    private readonly IReadOnlyList<IIncomingTransferHandler> _handlers;
    private readonly string _accountNumber;

    public IncomingTransferClassifier(IEnumerable<IIncomingTransferHandler> handlers, VietQrSettings vietQr)
    {
        // Tiền tố dài xét trước, để tiền tố ngắn là phần đầu của tiền tố dài không nuốt mất giao dịch.
        var ordered = handlers.OrderByDescending(h => h.Prefix.Length).ToList();
        var duplicate = ordered
            .GroupBy(h => h.Prefix, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(g => g.Count() > 1);
        if (duplicate != null)
        {
            throw new InvalidOperationException($"Có hai bộ xử lý giao dịch cùng tiền tố {duplicate.Key}.");
        }

        _handlers = ordered;
        _accountNumber = RemoveWhitespace(vietQr.AccountNumber);
    }

    public TransferClassification Classify(IncomingBankTransfer transfer)
    {
        if (!string.Equals(transfer.TransferType, IncomingType, StringComparison.Ordinal))
        {
            return new TransferClassification(TransferClassificationKind.Ignored, null, null);
        }

        // Chưa cấu hình số tài khoản thì không nhận giao dịch nào (fail closed).
        if (_accountNumber.Length == 0 ||
            !string.Equals(RemoveWhitespace(transfer.AccountNumber), _accountNumber, StringComparison.Ordinal))
        {
            return new TransferClassification(TransferClassificationKind.WrongAccount, null, null);
        }

        if (transfer.TransferAmount <= 0)
        {
            return new TransferClassification(TransferClassificationKind.Ignored, null, null);
        }

        var code = TransferCodeParser.Extract(transfer.Code, transfer.Content, transfer.Description);
        if (code == null)
        {
            return new TransferClassification(TransferClassificationKind.Unmatched, null, null);
        }

        var handler = _handlers.FirstOrDefault(h => code.StartsWith(h.Prefix, StringComparison.OrdinalIgnoreCase));
        return handler == null
            ? new TransferClassification(TransferClassificationKind.Unmatched, null, code)
            : new TransferClassification(TransferClassificationKind.Matched, handler, code);
    }

    private static string RemoveWhitespace(string? value)
    {
        return new string((value ?? string.Empty).Where(c => !char.IsWhiteSpace(c)).ToArray());
    }
}
