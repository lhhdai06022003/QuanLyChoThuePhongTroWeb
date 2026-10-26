using System.Globalization;
using Microsoft.Extensions.Logging;
using QuanLyChoThuePhongTroWeb.Application.Common.Configurations;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Payments;
using QuanLyChoThuePhongTroWeb.Application.Features.ReservationPayments;

namespace QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;

// IPN VNPay cho tiền giữ chỗ (spec giữ chỗ §6.2). Không có actorId và không kiểm phân quyền chi nhánh: tin cậy duy nhất nhờ
// chữ ký HMAC-SHA512 và vnp_TmnCode; sau đó ghi sổ bằng ReservationTransferHandler (cùng đường với webhook SePay).
// Cờ OnlinePayment:ReservationEnabled tắt thì không ghi (trả 01, VNPay gọi lại tối đa 10 lần).
// Không log tham số vnp_* khi sai chữ ký; không bao giờ log vnp_SecureHash, HashSecret hay vnp_OrderInfo.
public sealed class ReservationVnPayService(
    VnPayOptions options,
    OnlinePaymentOptions onlinePayment,
    IReservationAutoPaymentStore store,
    ReservationTransferHandler handler,
    IPaymentOutcomePublisher publisher,
    TimeProvider time,
    ILogger<ReservationVnPayService> logger) : IReservationVnPayService
{
    public async Task<VnPayIpnResult> HandleIpnAsync(IReadOnlyDictionary<string, string> query, CancellationToken ct = default)
    {
        // 1. Chữ ký.
        if (!VnPayProtocol.Verify(query, options.HashSecret))
        {
            logger.LogWarning("IPN VNPay giữ chỗ bị từ chối: thiếu hoặc sai chữ ký");
            return VnPayIpnResult.InvalidSignature;
        }

        // 2. Đúng merchant.
        if (!string.Equals(Read(query, VnPayProtocol.TmnCodeKey), options.TmnCode.Trim(), StringComparison.Ordinal))
        {
            logger.LogWarning("IPN VNPay giữ chỗ bị từ chối: vnp_TmnCode không khớp cấu hình");
            return VnPayIpnResult.InvalidSignature;
        }

        // 3. Mã đơn.
        var txnRef = Read(query, VnPayProtocol.TxnRefKey);
        if (!VnPayProtocol.TryParseReservationTxnRef(txnRef, out var yeuCauGiuChoId))
        {
            logger.LogInformation("IPN VNPay giữ chỗ: vnp_TxnRef không đúng định dạng");
            return VnPayIpnResult.OrderNotFound;
        }

        // 4. Giao dịch không thành công: đã nhận kết quả, không ghi sổ, không thông báo.
        var responseCode = Read(query, VnPayProtocol.ResponseCodeKey);
        var transactionStatus = Read(query, VnPayProtocol.TransactionStatusKey);
        if (responseCode != VnPayProtocol.SuccessCode || transactionStatus != VnPayProtocol.SuccessCode)
        {
            logger.LogInformation("IPN VNPay giữ chỗ: giao dịch không thành công, giữ chỗ {YeuCauGiuChoId}, ResponseCode {ResponseCode}, TransactionStatus {TransactionStatus}",
                yeuCauGiuChoId, responseCode, transactionStatus);
            return VnPayIpnResult.Confirmed;
        }

        // 4a. Cờ tắt: không ghi tự động. Trả 01 để VNPay gọi lại; bật cờ trong lúc đó thì lần sau ghi được.
        if (!onlinePayment.ReservationEnabled)
        {
            logger.LogWarning("IPN VNPay giữ chỗ: OnlinePayment:ReservationEnabled đang tắt, không ghi tiền giữ chỗ {YeuCauGiuChoId}, trả 01", yeuCauGiuChoId);
            return VnPayIpnResult.OrderNotFound;
        }

        // 4b. Giao dịch thành công cho giữ chỗ không có yêu cầu TTPTGC{id} (giữ chỗ mã cũ GC…, hoặc không tồn tại): "Order not found".
        if (!await store.PaymentRequestExistsAsync(ReservationTransferHandler.TransferCode(yeuCauGiuChoId), ct))
        {
            logger.LogWarning("IPN VNPay giữ chỗ: không có yêu cầu thanh toán cho giữ chỗ {YeuCauGiuChoId}, trả 01", yeuCauGiuChoId);
            return VnPayIpnResult.OrderNotFound;
        }

        // 5. Số tiền: chuỗi chữ số ASCII, dương, chia hết cho 100 (VNPay nhân 100).
        if (!long.TryParse(Read(query, VnPayProtocol.AmountKey), NumberStyles.None, CultureInfo.InvariantCulture, out var rawAmount)
            || rawAmount <= 0
            || rawAmount % 100 != 0)
        {
            logger.LogWarning("IPN VNPay giữ chỗ: vnp_Amount không hợp lệ, giữ chỗ {YeuCauGiuChoId}", yeuCauGiuChoId);
            return VnPayIpnResult.InvalidAmount;
        }

        // 5b. Không có khóa chống trùng thì không thể ghi an toàn; trả lỗi để VNPay gọi lại.
        var transactionNo = Read(query, VnPayProtocol.TransactionNoKey)?.Trim();
        if (string.IsNullOrEmpty(transactionNo)
            || InvoicePaymentPolicy.VnPayLedgerKey(transactionNo).Length > InvoicePaymentPolicy.MaxMaGiaoDichLength)
        {
            logger.LogWarning("IPN VNPay giữ chỗ: vnp_TransactionNo rỗng hoặc quá dài, giữ chỗ {YeuCauGiuChoId}", yeuCauGiuChoId);
            return VnPayIpnResult.UnknownError;
        }

        // 6. Ghi sổ. vnp_PayDate là giờ Việt Nam; thiếu hoặc sai định dạng thì dùng giờ server.
        var payDateUtc = VnPayProtocol.TryParseVietnamTime(Read(query, VnPayProtocol.PayDateKey), out var parsed)
            ? parsed
            : time.GetUtcNow().UtcDateTime;

        var transfer = new IncomingBankTransfer(
            SePayId: 0,
            Gateway: Read(query, VnPayProtocol.BankCodeKey),
            TransactionDateUtc: payDateUtc,
            AccountNumber: null,
            SubAccount: null,
            Code: null,
            Content: Read(query, VnPayProtocol.OrderInfoKey),
            TransferType: "in",
            TransferAmount: rawAmount / 100m,
            ReferenceCode: transactionNo,
            Description: txnRef)
        {
            Kenh = OnlinePaymentChannel.VnPay,
            CardType = Read(query, VnPayProtocol.CardTypeKey)
        };

        TransferOutcome outcome;
        try
        {
            outcome = await handler.HandleAsync(transfer, ReservationTransferHandler.TransferCode(yeuCauGiuChoId), ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "IPN VNPay giữ chỗ: lỗi khi ghi sổ, giữ chỗ {YeuCauGiuChoId}", yeuCauGiuChoId);
            return VnPayIpnResult.UnknownError;
        }

        logger.LogInformation("IPN VNPay giữ chỗ TXN {TransactionNo} → {Kind}, giữ chỗ {YeuCauGiuChoId}, số tiền {TransferAmount}",
            transactionNo, outcome.Kind, yeuCauGiuChoId, transfer.TransferAmount);

        // Sự kiện SignalR, Telegram, email. Lỗi ở đây không được làm hỏng kết quả đã ghi sổ.
        try
        {
            await publisher.PublishAsync(outcome, CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Không phát được thông báo cho IPN VNPay giữ chỗ TXN {TransactionNo}, giữ chỗ {YeuCauGiuChoId}", transactionNo, yeuCauGiuChoId);
        }

        switch (outcome.Kind)
        {
            case TransferOutcomeKind.Recorded:
                return VnPayIpnResult.Confirmed;

            case TransferOutcomeKind.Duplicate:
                return VnPayIpnResult.AlreadyConfirmed;

            // Yêu cầu biến mất giữa bước 4b và lúc ghi (không có xóa cứng nên không mong đợi).
            case TransferOutcomeKind.Unmatched:
                return VnPayIpnResult.OrderNotFound;

            case TransferOutcomeKind.Failed:
                logger.LogWarning("IPN VNPay giữ chỗ TXN {TransactionNo} → {Kind}, giữ chỗ {YeuCauGiuChoId}: {LyDo}",
                    transactionNo, outcome.Kind, yeuCauGiuChoId, outcome.LyDo);
                return VnPayIpnResult.UnknownError;

            default:
                logger.LogWarning("IPN VNPay giữ chỗ: kết quả ghi sổ không mong đợi {Kind}, giữ chỗ {YeuCauGiuChoId}", outcome.Kind, yeuCauGiuChoId);
                return VnPayIpnResult.UnknownError;
        }
    }

    private static string? Read(IReadOnlyDictionary<string, string> query, string key)
    {
        return query.TryGetValue(key, out var value) ? value : null;
    }
}
