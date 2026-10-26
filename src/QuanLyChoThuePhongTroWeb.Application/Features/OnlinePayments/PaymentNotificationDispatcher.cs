using System.Net.Mail;
using Microsoft.Extensions.Logging;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Services;
using QuanLyChoThuePhongTroWeb.Application.Common.Configurations;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Payments;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Persistence;

namespace QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;

// Recorded: tin thu tiền tới chat chung và chat của chi nhánh, email biên lai tới khách (chỉ dòng SePay hoặc VNPay).
// WrongAccount, Unmatched, InvoiceNotPayable, AlreadyPaid: cảnh báo Telegram, không gửi email.
// Lỗi từng chat hoặc từng email chỉ log Warning kèm hóa đơn và khoản; không log địa chỉ email hay nội dung chuyển khoản.
public sealed class PaymentNotificationDispatcher(
    IInvoicePaymentStore store,
    ITelegramNotifier telegram,
    IEmailService email,
    TelegramOptions telegramOptions,
    OnlinePaymentOptions onlinePaymentOptions,
    ILogger<PaymentNotificationDispatcher> logger) : IPaymentNotificationDispatcher
{
    public async Task DispatchAsync(PaymentNotificationWorkItem item, CancellationToken ct = default)
    {
        var outcome = item.Outcome;
        switch (outcome.Kind)
        {
            case TransferOutcomeKind.Recorded:
                await DispatchRecordedAsync(outcome, ct);
                break;

            case TransferOutcomeKind.WrongAccount:
            case TransferOutcomeKind.Unmatched:
            case TransferOutcomeKind.InvoiceNotPayable:
            case TransferOutcomeKind.AlreadyPaid:
                await DispatchAlertAsync(outcome, ct);
                break;
        }
    }

    private async Task DispatchRecordedAsync(TransferOutcome outcome, CancellationToken ct)
    {
        if (outcome.LichSuThanhToanId is not int lichSuThanhToanId)
        {
            logger.LogWarning("Không có khoản thanh toán để gửi thông báo cho {GiaoDich}, hóa đơn {HoaDonId}",
                outcome.Transfer.LogLabel, outcome.HoaDonId);
            return;
        }

        var receipt = await store.GetReceiptSnapshotAsync(lichSuThanhToanId, ct);
        if (receipt == null)
        {
            logger.LogWarning("Không tìm thấy khoản thanh toán {LichSuThanhToanId} (hóa đơn {HoaDonId}) để gửi thông báo",
                lichSuThanhToanId, outcome.HoaDonId);
            return;
        }

        if (telegramOptions.IsConfigured)
        {
            await SendTelegramAsync(
                telegramOptions.ResolveChatIds(receipt.ChiNhanhId),
                PaymentNotificationFormatter.FormatRecordedTelegram(outcome, receipt),
                receipt.HoaDonId, receipt.LichSuThanhToanId, ct);
        }

        await SendReceiptEmailAsync(outcome, receipt, ct);
    }

    private async Task DispatchAlertAsync(TransferOutcome outcome, CancellationToken ct)
    {
        if (!telegramOptions.IsConfigured)
        {
            return;
        }

        var context = outcome.HoaDonId.HasValue
            ? await store.GetTransferAlertContextAsync(outcome.HoaDonId.Value, ct)
            : null;

        await SendTelegramAsync(
            telegramOptions.ResolveChatIds(context?.ChiNhanhId),
            PaymentNotificationFormatter.FormatAlertTelegram(outcome, context),
            outcome.HoaDonId, outcome.LichSuThanhToanId, ct);
    }

    // Gửi lần lượt từng chat; chat lỗi (trả lỗi hoặc ném) không chặn các chat còn lại.
    private async Task SendTelegramAsync(
        IReadOnlyList<string> chatIds, string text, int? hoaDonId, int? lichSuThanhToanId, CancellationToken ct)
    {
        foreach (var chatId in chatIds)
        {
            try
            {
                var (isSuccess, errorMessage) = await telegram.SendMessageAsync(chatId, text, ct);
                if (!isSuccess)
                {
                    logger.LogWarning("Không gửi được tin Telegram: hóa đơn {HoaDonId}, khoản {LichSuThanhToanId}: {ErrorMessage}",
                        hoaDonId, lichSuThanhToanId, errorMessage);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Không gửi được tin Telegram: hóa đơn {HoaDonId}, khoản {LichSuThanhToanId}",
                    hoaDonId, lichSuThanhToanId);
            }
        }
    }

    // Chỉ tự gửi biên lai cho dòng SePay hoặc VNPay còn hiệu lực; thu thủ công không tự gửi (spec §6.3).
    private async Task SendReceiptEmailAsync(TransferOutcome outcome, PaymentReceiptSnapshot receipt, CancellationToken ct)
    {
        if (receipt.IsDeleted || !InvoicePaymentPolicy.IsSystemRecordedKey(receipt.MaGiaoDich))
        {
            return;
        }

        var toEmail = receipt.EmailKhachThue?.Trim();
        if (string.IsNullOrEmpty(toEmail) || !MailAddress.TryCreate(toEmail, out _))
        {
            logger.LogWarning("Khách thuê không có email hợp lệ để gửi biên lai: hóa đơn {HoaDonId}, khoản {LichSuThanhToanId}",
                receipt.HoaDonId, receipt.LichSuThanhToanId);
            return;
        }

        var data = PaymentNotificationFormatter.BuildReceiptEmail(
            receipt,
            outcome.Transfer.ReferenceCode,
            onlinePaymentOptions.BuildUrl($"/KhachThue/HoaDon/BienLai/{receipt.LichSuThanhToanId}"));

        try
        {
            var (isSuccess, errorMessage) = await email.SendPaymentReceiptAsync(toEmail, data);
            if (!isSuccess)
            {
                logger.LogWarning("Không gửi được email biên lai: hóa đơn {HoaDonId}, khoản {LichSuThanhToanId}: {ErrorMessage}",
                    receipt.HoaDonId, receipt.LichSuThanhToanId, errorMessage);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Không gửi được email biên lai: hóa đơn {HoaDonId}, khoản {LichSuThanhToanId}",
                receipt.HoaDonId, receipt.LichSuThanhToanId);
        }
    }
}
