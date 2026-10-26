using System.Globalization;
using Microsoft.Extensions.Logging;
using QuanLyChoThuePhongTroWeb.Application.Common.Configurations;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Payments;

namespace QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;

// IPN VNPay (spec VNPay §5.2). Không có actorId và không kiểm phân quyền chi nhánh: tin cậy duy nhất nhờ chữ ký
// HMAC-SHA512 và vnp_TmnCode; sau đó ghi sổ bằng InvoiceTransferHandler như Admin (giống webhook SePay).
// Không log tham số vnp_* khi sai chữ ký; không bao giờ log vnp_SecureHash, HashSecret hay vnp_OrderInfo.
public sealed class VnPayPaymentService(
    VnPayOptions options,
    InvoiceTransferHandler handler,
    IPaymentOutcomePublisher publisher,
    TimeProvider time,
    ILogger<VnPayPaymentService> logger) : IVnPayPaymentService
{
    public async Task<VnPayIpnResult> HandleIpnAsync(IReadOnlyDictionary<string, string> query, CancellationToken ct = default)
    {
        // 1. Chữ ký.
        if (!VnPayProtocol.Verify(query, options.HashSecret))
        {
            logger.LogWarning("IPN VNPay bị từ chối: thiếu hoặc sai chữ ký");
            return VnPayIpnResult.InvalidSignature;
        }

        // 2. Đúng merchant.
        if (!string.Equals(Read(query, VnPayProtocol.TmnCodeKey), options.TmnCode.Trim(), StringComparison.Ordinal))
        {
            logger.LogWarning("IPN VNPay bị từ chối: vnp_TmnCode không khớp cấu hình");
            return VnPayIpnResult.InvalidSignature;
        }

        // 3. Mã đơn.
        var txnRef = Read(query, VnPayProtocol.TxnRefKey);
        if (!VnPayProtocol.TryParseTxnRef(txnRef, out var hoaDonId))
        {
            logger.LogInformation("IPN VNPay: vnp_TxnRef không đúng định dạng");
            return VnPayIpnResult.OrderNotFound;
        }

        // 4. Giao dịch không thành công: đã nhận kết quả, không ghi sổ, không thông báo.
        var responseCode = Read(query, VnPayProtocol.ResponseCodeKey);
        var transactionStatus = Read(query, VnPayProtocol.TransactionStatusKey);
        if (responseCode != VnPayProtocol.SuccessCode || transactionStatus != VnPayProtocol.SuccessCode)
        {
            logger.LogInformation("IPN VNPay: giao dịch không thành công, hóa đơn {HoaDonId}, ResponseCode {ResponseCode}, TransactionStatus {TransactionStatus}",
                hoaDonId, responseCode, transactionStatus);
            return VnPayIpnResult.Confirmed;
        }

        // 5. Số tiền: chuỗi chữ số ASCII, dương, chia hết cho 100 (VNPay nhân 100).
        if (!long.TryParse(Read(query, VnPayProtocol.AmountKey), NumberStyles.None, CultureInfo.InvariantCulture, out var rawAmount)
            || rawAmount <= 0
            || rawAmount % 100 != 0)
        {
            logger.LogWarning("IPN VNPay: vnp_Amount không hợp lệ, hóa đơn {HoaDonId}", hoaDonId);
            return VnPayIpnResult.InvalidAmount;
        }

        // 5b. Không có khóa chống trùng thì không thể ghi an toàn; trả lỗi để VNPay gọi lại.
        var transactionNo = Read(query, VnPayProtocol.TransactionNoKey)?.Trim();
        if (string.IsNullOrEmpty(transactionNo)
            || InvoicePaymentPolicy.VnPayLedgerKey(transactionNo).Length > InvoicePaymentPolicy.MaxMaGiaoDichLength)
        {
            logger.LogWarning("IPN VNPay: vnp_TransactionNo rỗng hoặc quá dài, hóa đơn {HoaDonId}", hoaDonId);
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
            outcome = await handler.HandleAsync(transfer, InvoiceTransferHandler.TransferCode(hoaDonId), ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "IPN VNPay: lỗi khi ghi sổ, hóa đơn {HoaDonId}", hoaDonId);
            return VnPayIpnResult.UnknownError;
        }

        if (outcome.Kind == TransferOutcomeKind.Failed)
        {
            logger.LogWarning("IPN VNPay → {Kind}, hóa đơn {HoaDonId}: {LyDo}", outcome.Kind, outcome.HoaDonId, outcome.LyDo);
        }
        else if (outcome.Kind == TransferOutcomeKind.Recorded && outcome.ConMinhChungChoDoiChieu)
        {
            // Dấu vết cho nhân viên kiểm trùng trước khi xác nhận minh chứng (thông báo trong app cũng có câu này).
            logger.LogWarning("IPN VNPay TXN {TransactionNo} → {Kind}, hóa đơn {HoaDonId} ghi {SoTienGhiNhan}, tiền thừa {TienThua}, còn lại {ConLai}; hóa đơn còn minh chứng chờ đối chiếu, kiểm trùng trước khi xác nhận",
                transactionNo, outcome.Kind, outcome.HoaDonId, outcome.SoTienGhiNhan, outcome.TienThua, outcome.ConLai);
        }
        else
        {
            logger.LogInformation("IPN VNPay TXN {TransactionNo} → {Kind}, hóa đơn {HoaDonId}, số tiền {TransferAmount}, ghi {SoTienGhiNhan}, tiền thừa {TienThua}, còn lại {ConLai}",
                transactionNo, outcome.Kind, outcome.HoaDonId, transfer.TransferAmount,
                outcome.SoTienGhiNhan, outcome.TienThua, outcome.ConLai);
        }

        // Sự kiện SignalR, Telegram, email biên lai. Lỗi ở đây không được làm hỏng kết quả đã ghi sổ.
        try
        {
            await publisher.PublishAsync(outcome, CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Không phát được thông báo cho IPN VNPay TXN {TransactionNo}, hóa đơn {HoaDonId}", transactionNo, outcome.HoaDonId);
        }

        switch (outcome.Kind)
        {
            case TransferOutcomeKind.Recorded:
                return VnPayIpnResult.Confirmed;

            case TransferOutcomeKind.Duplicate:
            case TransferOutcomeKind.InvoiceNotPayable:
            case TransferOutcomeKind.AlreadyPaid:
                return VnPayIpnResult.AlreadyConfirmed;

            case TransferOutcomeKind.Failed:
                return VnPayIpnResult.UnknownError;

            default:
                logger.LogWarning("IPN VNPay: kết quả ghi sổ không mong đợi {Kind}, hóa đơn {HoaDonId}", outcome.Kind, outcome.HoaDonId);
                return VnPayIpnResult.UnknownError;
        }
    }

    private static string? Read(IReadOnlyDictionary<string, string> query, string key)
    {
        return query.TryGetValue(key, out var value) ? value : null;
    }
}
