using Microsoft.Extensions.Logging;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Notifications;

namespace QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;

// Recorded: gửi ngay sự kiện HoaDonThanhToanCapNhat cho khách rồi đưa việc Telegram + email biên lai vào hàng đợi.
// WrongAccount, Unmatched, InvoiceNotPayable, AlreadyPaid: đưa cảnh báo Telegram vào hàng đợi, mỗi giao dịch một lần
// trong 24 giờ (spec §5.3); riêng giao dịch của đồng bộ bù (LaDongBoBu) không cảnh báo vì Admin đang xem bảng kết quả.
// Duplicate, Ignored, Failed: không làm gì.
// Mọi lỗi chỉ log Warning kèm kênh và khóa chống trùng của giao dịch, hóa đơn và khoản; không log nội dung chuyển khoản.
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
                await SendReservationPaymentUpdatedAsync(outcome);
                await EnqueueAsync(outcome, ct);
                break;

            case TransferOutcomeKind.WrongAccount:
            case TransferOutcomeKind.Unmatched:
            case TransferOutcomeKind.InvoiceNotPayable:
            case TransferOutcomeKind.AlreadyPaid:
                // Đồng bộ bù: không cảnh báo và không đăng ký khóa với bộ chống lặp, để webhook gửi lại sau đó vẫn được cảnh báo.
                if (outcome.Transfer.LaDongBoBu)
                {
                    break;
                }

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
            logger.LogWarning(ex, "Không gửi được sự kiện cập nhật thanh toán cho {GiaoDich}, hóa đơn {HoaDonId}, khoản {LichSuThanhToanId}",
                outcome.Transfer.LogLabel, outcome.HoaDonId, outcome.LichSuThanhToanId);
        }
    }

    // Tiền giữ chỗ được ghi tự động: báo trang đặt cọc của khách (spec giữ chỗ §8). Giao dịch hóa đơn không có GiuCho.
    private async Task SendReservationPaymentUpdatedAsync(TransferOutcome outcome)
    {
        if (outcome.GiuCho is not { } giuCho)
        {
            return;
        }

        try
        {
            await notifier.SendReservationPaymentUpdatedAsync(
                giuCho.GuestUserId,
                new ReservationPaymentUpdatedEvent(giuCho.YeuCauGiuChoId, giuCho.DaNhanSau, outcome.ConLai, giuCho.TrangThaiGiuCho),
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Không gửi được sự kiện cập nhật tiền giữ chỗ cho {GiaoDich}, giữ chỗ {YeuCauGiuChoId}, giao dịch {GiaoDichGiuChoId}",
                outcome.Transfer.LogLabel, giuCho.YeuCauGiuChoId, giuCho.GiaoDichGiuChoId);
        }
    }

    private async Task EnqueueAlertOnceAsync(TransferOutcome outcome, CancellationToken ct)
    {
        try
        {
            if (!deduplicator.TryRegister(outcome.DedupKey))
            {
                logger.LogInformation("Bỏ qua cảnh báo lặp cho {GiaoDich} → {Kind}", outcome.Transfer.LogLabel, outcome.Kind);
                return;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Không kiểm tra được cảnh báo lặp cho {GiaoDich}, hóa đơn {HoaDonId}",
                outcome.Transfer.LogLabel, outcome.HoaDonId);
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
                logger.LogWarning("Hàng đợi thông báo thanh toán đầy, bỏ tin Telegram và email của {GiaoDich}, hóa đơn {HoaDonId}, khoản {LichSuThanhToanId}",
                    outcome.Transfer.LogLabel, outcome.HoaDonId, outcome.LichSuThanhToanId);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Không đưa được thông báo vào hàng đợi cho {GiaoDich}, hóa đơn {HoaDonId}, khoản {LichSuThanhToanId}",
                outcome.Transfer.LogLabel, outcome.HoaDonId, outcome.LichSuThanhToanId);
        }
    }
}
