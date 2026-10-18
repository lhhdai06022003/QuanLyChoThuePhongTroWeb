using Microsoft.Extensions.Logging;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Notifications;

namespace QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;

// Recorded: gửi ngay sự kiện HoaDonThanhToanCapNhat cho khách rồi đưa việc Telegram + email biên lai vào hàng đợi.
// WrongAccount, Unmatched, InvoiceNotPayable, AlreadyPaid: đưa cảnh báo Telegram vào hàng đợi, mỗi giao dịch một lần
// trong 24 giờ (spec §5.3). Duplicate, Ignored, Failed: không làm gì.
// Mọi lỗi chỉ log Warning kèm mã giao dịch SePay, hóa đơn và khoản; không log nội dung chuyển khoản.
public sealed class PaymentOutcomePublisher(
    IThongBaoNotifier notifier,
    IPaymentNotificationQueue queue,
    ITransferAlertDeduplicator deduplicator,
    ILogger<PaymentOutcomePublisher> logger) : IPaymentOutcomePublisher
{
    public async Task PublishAsync(TransferOutcome outcome, CancellationToken ct = default)
    {
        switch (outcome.Kind)
        {
            case TransferOutcomeKind.Recorded:
                await SendInvoicePaymentUpdatedAsync(outcome);
                await EnqueueAsync(outcome, ct);
                break;

            case TransferOutcomeKind.WrongAccount:
            case TransferOutcomeKind.Unmatched:
            case TransferOutcomeKind.InvoiceNotPayable:
            case TransferOutcomeKind.AlreadyPaid:
                await EnqueueAlertOnceAsync(outcome, ct);
                break;
        }
    }

    private async Task SendInvoicePaymentUpdatedAsync(TransferOutcome outcome)
    {
        if (outcome.TenantUserId is not int tenantUserId || outcome.HoaDonId is not int hoaDonId)
        {
            return;
        }

        try
        {
            await notifier.SendInvoicePaymentUpdatedAsync(
                tenantUserId,
                new InvoicePaymentUpdatedEvent(hoaDonId, outcome.ConLai, outcome.TrangThaiThanhToan ?? string.Empty),
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Không gửi được sự kiện cập nhật thanh toán cho SePay {SePayId}, hóa đơn {HoaDonId}, khoản {LichSuThanhToanId}",
                outcome.Transfer.SePayId, outcome.HoaDonId, outcome.LichSuThanhToanId);
        }
    }

    private async Task EnqueueAlertOnceAsync(TransferOutcome outcome, CancellationToken ct)
    {
        try
        {
            if (!deduplicator.TryRegister(outcome.DedupKey))
            {
                logger.LogInformation("Bỏ qua cảnh báo lặp cho SePay {SePayId} → {Kind}", outcome.Transfer.SePayId, outcome.Kind);
                return;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Không kiểm tra được cảnh báo lặp cho SePay {SePayId}, hóa đơn {HoaDonId}",
                outcome.Transfer.SePayId, outcome.HoaDonId);
            return;
        }

        await EnqueueAsync(outcome, ct);
    }

    private async Task EnqueueAsync(TransferOutcome outcome, CancellationToken ct)
    {
        try
        {
            if (!await queue.EnqueueAsync(new PaymentNotificationWorkItem(outcome), ct))
            {
                logger.LogWarning("Hàng đợi thông báo thanh toán đầy, bỏ tin Telegram và email của SePay {SePayId}, hóa đơn {HoaDonId}, khoản {LichSuThanhToanId}",
                    outcome.Transfer.SePayId, outcome.HoaDonId, outcome.LichSuThanhToanId);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Không đưa được thông báo vào hàng đợi cho SePay {SePayId}, hóa đơn {HoaDonId}, khoản {LichSuThanhToanId}",
                outcome.Transfer.SePayId, outcome.HoaDonId, outcome.LichSuThanhToanId);
        }
    }
}
