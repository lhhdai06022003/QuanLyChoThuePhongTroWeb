using Microsoft.Extensions.Logging;

namespace QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;

public sealed class BankTransferIngestionService(
    IncomingTransferClassifier classifier,
    ILogger<BankTransferIngestionService> logger) : IBankTransferIngestionService
{
    public async Task<TransferOutcome> IngestAsync(IncomingBankTransfer transfer, CancellationToken ct = default)
    {
        var classification = classifier.Classify(transfer);
        var outcome = classification.Kind == TransferClassificationKind.Matched
            ? await classification.Handler!.HandleAsync(transfer, classification.Code!, ct)
            : new TransferOutcome
            {
                Kind = classification.Kind switch
                {
                    TransferClassificationKind.WrongAccount => TransferOutcomeKind.WrongAccount,
                    TransferClassificationKind.Unmatched => TransferOutcomeKind.Unmatched,
                    _ => TransferOutcomeKind.Ignored
                },
                Transfer = transfer,
                Code = classification.Code
            };

        // Không log nội dung chuyển khoản.
        if (outcome.Kind == TransferOutcomeKind.Failed)
        {
            logger.LogWarning("SePay {SePayId} → {Kind}, mã {Code}, hóa đơn {HoaDonId}: {LyDo}",
                transfer.SePayId, outcome.Kind, outcome.Code, outcome.HoaDonId, outcome.LyDo);
        }
        else
        {
            logger.LogInformation("SePay {SePayId} → {Kind}, mã {Code}, hóa đơn {HoaDonId}, số tiền {TransferAmount}",
                transfer.SePayId, outcome.Kind, outcome.Code, outcome.HoaDonId, transfer.TransferAmount);
        }

        return outcome;
    }
}
