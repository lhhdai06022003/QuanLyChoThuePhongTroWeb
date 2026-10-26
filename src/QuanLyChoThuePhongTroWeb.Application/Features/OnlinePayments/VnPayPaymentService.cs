using System.Globalization;
using Microsoft.Extensions.Logging;
using QuanLyChoThuePhongTroWeb.Application.Common.Configurations;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Payments;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Persistence;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;

// IPN VNPay (spec VNPay §5.2). Không có actorId và không kiểm phân quyền chi nhánh: tin cậy duy nhất nhờ chữ ký
// HMAC-SHA512 và vnp_TmnCode; sau đó ghi sổ bằng InvoiceTransferHandler như Admin (giống webhook SePay).
// Tạo link (§4.2) và trang kết quả (§4.3) chỉ đọc: không mở transaction, không khóa, không ghi; chỉ IPN được ghi sổ.
// Không log tham số vnp_* khi sai chữ ký; không bao giờ log vnp_SecureHash, HashSecret, URL thanh toán hay vnp_OrderInfo.
public sealed class VnPayPaymentService(
    VnPayOptions options,
    IInvoicePaymentStore store,
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

    public async Task<ServiceResult<string>> CreatePaymentUrlAsync(
        int hoaDonId, int nguoiThueId, string clientIp, string returnUrl, CancellationToken ct = default)
    {
        const string notFoundMessage = "Không tìm thấy hóa đơn.";

        var target = await store.GetTargetByHoaDonAsync(hoaDonId, ct);
        if (target == null || !target.KhachXemDuoc(nguoiThueId))
        {
            return ServiceResult<string>.NotFound(notFoundMessage);
        }

        var invoice = await store.GetInvoiceSnapshotAsync(hoaDonId, ct);
        if (invoice == null)
        {
            return ServiceResult<string>.NotFound(notFoundMessage);
        }

        // Cùng lý do và câu chữ với panel QR (TenantPaymentPanelService), không gồm điều kiện tài khoản VietQR.
        var conLai = invoice.TongTien - invoice.DaThu;
        if (invoice.IsDeleted || invoice.TrangThaiPhatHanh != TrangThaiPhatHanhHoaDon.DaGui)
        {
            return ServiceResult<string>.Fail("Hóa đơn không còn ở trạng thái nhận thanh toán.");
        }

        if (conLai <= 0)
        {
            return ServiceResult<string>.Fail("Hóa đơn đã được thanh toán đủ.");
        }

        // vnp_Amount phải là số nguyên: tổng tiền hoặc số còn lại lẻ (dữ liệu cũ) thì không tạo link.
        if (!InvoiceMoney.IsWholeVnd(invoice.TongTien) || !InvoiceMoney.IsWholeVnd(conLai))
        {
            return ServiceResult<string>.Fail("Hóa đơn có số tiền lẻ, vui lòng liên hệ quản lý để lập lại hóa đơn.");
        }

        var nowUtc = time.GetUtcNow().UtcDateTime;
        var txnRef = VnPayProtocol.BuildTxnRef(hoaDonId, nowUtc);
        var parameters = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [VnPayProtocol.VersionKey] = VnPayProtocol.Version,
            [VnPayProtocol.CommandKey] = VnPayProtocol.PayCommand,
            [VnPayProtocol.TmnCodeKey] = options.TmnCode.Trim(),
            [VnPayProtocol.AmountKey] = decimal.ToInt64(conLai * 100m).ToString(CultureInfo.InvariantCulture),
            [VnPayProtocol.CurrCodeKey] = VnPayProtocol.CurrCode,
            [VnPayProtocol.TxnRefKey] = txnRef,
            [VnPayProtocol.OrderInfoKey] = VnPayProtocol.BuildOrderInfo(invoice.MaHoaDon),
            [VnPayProtocol.OrderTypeKey] = VnPayProtocol.OrderType,
            [VnPayProtocol.LocaleKey] = VnPayProtocol.Locale,
            [VnPayProtocol.ReturnUrlKey] = returnUrl,
            [VnPayProtocol.IpAddrKey] = clientIp,
            [VnPayProtocol.CreateDateKey] = VnPayProtocol.FormatVietnamTime(nowUtc),
            [VnPayProtocol.ExpireDateKey] = VnPayProtocol.FormatVietnamTime(nowUtc.AddMinutes(options.ExpireMinutes))
        };

        logger.LogInformation("Tạo link VNPay cho hóa đơn {HoaDonId}, mã đơn {TxnRef}", hoaDonId, txnRef);
        return ServiceResult<string>.Ok(VnPayProtocol.BuildPaymentUrl(options.PaymentUrl, parameters, options.HashSecret));
    }

    public async Task<VnPayReturnResult> GetReturnResultAsync(
        IReadOnlyDictionary<string, string> query, int nguoiThueId, CancellationToken ct = default)
    {
        // Mọi trường hợp không tin được đều trả cùng một kết quả, không kèm dữ liệu hóa đơn.
        if (!VnPayProtocol.Verify(query, options.HashSecret)
            || !string.Equals(Read(query, VnPayProtocol.TmnCodeKey), options.TmnCode.Trim(), StringComparison.Ordinal)
            || !VnPayProtocol.TryParseTxnRef(Read(query, VnPayProtocol.TxnRefKey), out var hoaDonId))
        {
            logger.LogInformation("Trang kết quả VNPay: không xác thực được tham số trả về");
            return VnPayReturnResult.Unverified;
        }

        var target = await store.GetTargetByHoaDonAsync(hoaDonId, ct);
        if (target == null || !target.KhachXemDuoc(nguoiThueId))
        {
            logger.LogInformation("Trang kết quả VNPay: không xác thực được tham số trả về");
            return VnPayReturnResult.Unverified;
        }

        var responseCode = Read(query, VnPayProtocol.ResponseCodeKey)?.Trim();
        if (string.IsNullOrEmpty(responseCode))
        {
            return VnPayReturnResult.Unverified;
        }

        if (responseCode == VnPayProtocol.CancelledCode)
        {
            return new VnPayReturnResult { TrangThai = VnPayReturnStatus.DaHuy, HoaDonId = hoaDonId };
        }

        if (responseCode != VnPayProtocol.SuccessCode)
        {
            return new VnPayReturnResult
            {
                TrangThai = VnPayReturnStatus.KhongThanhCong,
                HoaDonId = hoaDonId,
                MoTaLoi = VnPayResponseCodes.Describe(responseCode)
            };
        }

        // Không có khóa chống trùng thì IPN cũng không ghi được (cùng điều kiện bước 5b của IPN).
        var transactionNo = Read(query, VnPayProtocol.TransactionNoKey)?.Trim();
        if (string.IsNullOrEmpty(transactionNo))
        {
            return VnPayReturnResult.Unverified;
        }

        var ledgerKey = InvoicePaymentPolicy.VnPayLedgerKey(transactionNo);
        if (ledgerKey.Length > InvoicePaymentPolicy.MaxMaGiaoDichLength)
        {
            return VnPayReturnResult.Unverified;
        }

        var recorded = await FindActiveLedgerRowAsync(hoaDonId, ledgerKey, ct);
        if (recorded == null && await WillNeverBeRecordedAsync(hoaDonId, ledgerKey, ct))
        {
            // IPN có thể commit giữa lần đọc sổ ở trên và lần đọc hóa đơn (hóa đơn vừa trả đủ, khóa vừa tồn tại):
            // đọc sổ lại lần cuối để không báo nhầm "không ghi nhận".
            recorded = await FindActiveLedgerRowAsync(hoaDonId, ledgerKey, ct);
            if (recorded == null)
            {
                return new VnPayReturnResult { TrangThai = VnPayReturnStatus.KhongGhiNhan, HoaDonId = hoaDonId };
            }
        }

        if (recorded == null)
        {
            return new VnPayReturnResult { TrangThai = VnPayReturnStatus.DangGhiNhan, HoaDonId = hoaDonId };
        }

        // Số đã ghi sổ nhỏ hơn số khách trả khi có tiền thừa (spec VNPay §6, bước 4): cho khách biết cả hai.
        var paid = ReadPaidAmount(query);
        return new VnPayReturnResult
        {
            TrangThai = VnPayReturnStatus.DaGhiNhan,
            HoaDonId = hoaDonId,
            LichSuThanhToanId = recorded.LichSuThanhToanId,
            SoTien = recorded.SoTienThanhToan,
            SoTienVnPayBao = paid.HasValue && paid.Value != recorded.SoTienThanhToan ? paid : null
        };
    }

    private async Task<PaymentLedgerRowSnapshot?> FindActiveLedgerRowAsync(int hoaDonId, string ledgerKey, CancellationToken ct)
    {
        var rows = await store.GetActiveLedgerRowsAsync(hoaDonId, ct);
        return rows.FirstOrDefault(r => string.Equals(r.MaGiaoDich, ledgerKey, StringComparison.OrdinalIgnoreCase));
    }

    // IPN sẽ không ghi dòng này: hóa đơn không còn nhận tiền (IPN trả 02), hoặc dòng đã bị hủy ghi nhận
    // (bước 1 của §6 tính cả dòng xóa mềm).
    private async Task<bool> WillNeverBeRecordedAsync(int hoaDonId, string ledgerKey, CancellationToken ct)
    {
        var invoice = await store.GetInvoiceSnapshotAsync(hoaDonId, ct);
        if (invoice == null
            || invoice.IsDeleted
            || invoice.TrangThaiPhatHanh != TrangThaiPhatHanhHoaDon.DaGui
            || invoice.TongTien - invoice.DaThu <= 0)
        {
            return true;
        }

        return await store.ExistsMaGiaoDichForInvoiceIncludingDeletedAsync(hoaDonId, ledgerKey, ct);
    }

    // vnp_Amount / 100 khi là chuỗi chữ số dương chia hết cho 100; ngược lại null.
    private static decimal? ReadPaidAmount(IReadOnlyDictionary<string, string> query)
    {
        return long.TryParse(Read(query, VnPayProtocol.AmountKey), NumberStyles.None, CultureInfo.InvariantCulture, out var raw)
            && raw > 0
            && raw % 100 == 0
                ? raw / 100m
                : null;
    }

    private static string? Read(IReadOnlyDictionary<string, string> query, string key)
    {
        return query.TryGetValue(key, out var value) ? value : null;
    }
}
