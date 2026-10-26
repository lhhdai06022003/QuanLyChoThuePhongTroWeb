using System.Globalization;
using Microsoft.Extensions.Logging;
using QuanLyChoThuePhongTroWeb.Application.Common.Configurations;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Payments;
using QuanLyChoThuePhongTroWeb.Application.Features.ReservationPayments;
using QuanLyChoThuePhongTroWeb.Application.Features.ThongBaos.Services;

namespace QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;

// Ghi tiền giữ chỗ khi có tiền vào với mã TTPTGC{YeuCauGiuChoId} (spec giữ chỗ §6).
// Không có actorId và không kiểm phân quyền chi nhánh: chỉ được gọi từ IBankTransferIngestionService, nơi webhook đã xác thực
// chữ ký SePay (đồng bộ bù không ghi, X9), và từ IReservationVnPayService, nơi IPN đã kiểm chữ ký VNPay.
// Cờ OnlinePayment:ReservationEnabled tắt thì không ghi tự động (Unmatched, nhân viên đối soát tay).
public sealed class ReservationTransferHandler(
    IReservationAutoPaymentStore store,
    IThongBaoService thongBao,
    OnlinePaymentOptions onlinePayment,
    TimeProvider time,
    ILogger<ReservationTransferHandler> logger) : IIncomingTransferHandler
{
    public const string ReservationPrefix = "TTPTGC";

    private const string NotFoundReason = "Không tìm thấy yêu cầu thanh toán giữ chỗ";
    private const string SyncIgnoredReason = "Giao dịch giữ chỗ: đồng bộ bù không ghi, đối chiếu tay ở màn Giữ chỗ.";
    private const int MaxHistoryReasonLength = 500;

    public string Prefix => ReservationPrefix;

    // Mã chuyển khoản của yêu cầu thanh toán giữ chỗ (TTPTGC{YeuCauGiuChoId}), dùng chung cho QR, ApproveAsync và bộ phân loại.
    public static string TransferCode(int yeuCauGiuChoId) =>
        ReservationPrefix + yeuCauGiuChoId.ToString(CultureInfo.InvariantCulture);

    public async Task<TransferOutcome> HandleAsync(IncomingBankTransfer transfer, string code, CancellationToken ct = default)
    {
        // Bộ phân loại đã loại số tiền không dương; kiểm lại để không bao giờ ghi khoản 0đ hoặc âm.
        if (transfer.TransferAmount <= 0)
        {
            return new TransferOutcome { Kind = TransferOutcomeKind.Ignored, Transfer = transfer, Code = code };
        }

        var yeuCauGiuChoId = TransferCodeParser.ExtractReservationId(code);
        if (yeuCauGiuChoId is not int id)
        {
            return new TransferOutcome { Kind = TransferOutcomeKind.Unmatched, Transfer = transfer, Code = code };
        }

        if (transfer.LaDongBoBu)
        {
            return new TransferOutcome
            {
                Kind = TransferOutcomeKind.Ignored,
                Transfer = transfer,
                Code = code,
                LyDo = SyncIgnoredReason
            };
        }

        if (!onlinePayment.ReservationEnabled)
        {
            return new TransferOutcome { Kind = TransferOutcomeKind.Unmatched, Transfer = transfer, Code = code };
        }

        var dedupKey = transfer.DedupKey;
        if (dedupKey.Length > InvoicePaymentPolicy.MaxMaGiaoDichLength)
        {
            return new TransferOutcome
            {
                Kind = TransferOutcomeKind.Failed,
                Transfer = transfer,
                Code = code,
                LyDo = "Mã giao dịch quá dài"
            };
        }

        SystemReservationRecordResult result;
        try
        {
            result = await store.RecordSystemTransferAsync(
                TransferCode(id),
                dedupKey,
                transfer.TransferAmount,
                transfer.TransactionDateUtc,
                Truncate("Ghi nhận tự động " + dedupKey, MaxHistoryReasonLength),
                time.GetUtcNow().UtcDateTime,
                ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Lỗi khi ghi tiền giữ chỗ cho {GiaoDich}, giữ chỗ {YeuCauGiuChoId}", transfer.LogLabel, id);
            return new TransferOutcome
            {
                Kind = TransferOutcomeKind.Failed,
                Transfer = transfer,
                Code = code,
                LyDo = "Lỗi hệ thống khi ghi tiền giữ chỗ"
            };
        }

        switch (result.Status)
        {
            case SystemReservationRecordStatus.NotFound:
                return new TransferOutcome
                {
                    Kind = TransferOutcomeKind.Unmatched,
                    Transfer = transfer,
                    Code = code,
                    LyDo = NotFoundReason
                };

            case SystemReservationRecordStatus.Duplicate:
                return new TransferOutcome { Kind = TransferOutcomeKind.Duplicate, Transfer = transfer, Code = code };

            case SystemReservationRecordStatus.Conflict:
                return new TransferOutcome
                {
                    Kind = TransferOutcomeKind.Failed,
                    Transfer = transfer,
                    Code = code,
                    LyDo = "Dữ liệu vừa thay đổi"
                };
        }

        var conLai = Math.Max(0m, result.SoTienGiuCho - result.DaNhanSau);
        // Khoản về muộn (cần hoàn) tính riêng; chỉ phần vượt số giữ chỗ của lần này mới là tiền thừa. Chỉ để hiển thị.
        var tienThua = result.CanHoanTien
            ? 0m
            : Math.Max(0m, Math.Min(transfer.TransferAmount, result.DaNhanSau - result.SoTienGiuCho));

        var outcome = new TransferOutcome
        {
            Kind = TransferOutcomeKind.Recorded,
            Transfer = transfer,
            Code = TransferCode(id),
            SoTienGhiNhan = transfer.TransferAmount,
            TienThua = tienThua,
            TongTien = result.SoTienGiuCho,
            DaThu = result.DaNhanSau,
            ConLai = conLai,
            GiuCho = result
        };

        await NotifyStaffAsync(outcome, id, ct);

        if (result.ConMinhChungChoXacNhan)
        {
            // Dấu vết cho nhân viên kiểm trùng trước khi xác nhận minh chứng (thông báo trong app cũng có câu này).
            logger.LogWarning("Tiền giữ chỗ {GiaoDich}: giữ chỗ {YeuCauGiuChoId} ghi {SoTienGhiNhan}, đã nhận {DaNhan}, còn lại {ConLai}, cần hoàn {CanHoan}; còn minh chứng chờ xác nhận, kiểm trùng trước khi xác nhận",
                transfer.LogLabel, id, outcome.SoTienGhiNhan, result.DaNhanSau, conLai, result.CanHoanTien);
        }
        else
        {
            logger.LogInformation("Tiền giữ chỗ {GiaoDich}: giữ chỗ {YeuCauGiuChoId} ghi {SoTienGhiNhan}, đã nhận {DaNhan}, còn lại {ConLai}, cần hoàn {CanHoan}",
                transfer.LogLabel, id, outcome.SoTienGhiNhan, result.DaNhanSau, conLai, result.CanHoanTien);
        }

        return outcome;
    }

    // Báo Admin và nhân viên phụ trách phòng. Lỗi chỉ log Warning, không hoàn tác khoản đã ghi.
    private async Task NotifyStaffAsync(TransferOutcome outcome, int yeuCauGiuChoId, CancellationToken ct)
    {
        var giuCho = outcome.GiuCho!;
        try
        {
            var sent = await thongBao.GuiChoNguoiPhuTrachPhongAsync(
                giuCho.PhongTroId,
                ReservationPaymentNotificationFormatter.StaffTitle(outcome),
                ReservationPaymentNotificationFormatter.StaffContent(outcome),
                "ThanhToan",
                $"/QuanLyNhaTro/GiuCho/ChiTiet/{yeuCauGiuChoId}",
                ct);
            if (!sent.Success)
            {
                logger.LogWarning("Không gửi được thông báo trong app cho tiền giữ chỗ {GiaoDich}, giữ chỗ {YeuCauGiuChoId}: {Message}",
                    outcome.Transfer.LogLabel, yeuCauGiuChoId, sent.Message);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Không gửi được thông báo trong app cho tiền giữ chỗ {GiaoDich}, giữ chỗ {YeuCauGiuChoId}",
                outcome.Transfer.LogLabel, yeuCauGiuChoId);
        }
    }

    // Không cắt giữa cặp surrogate: chuỗi UTF-16 hỏng làm Npgsql ném lỗi khi ghi.
    private static string Truncate(string value, int maxLength)
    {
        if (value.Length <= maxLength)
        {
            return value;
        }

        var length = char.IsHighSurrogate(value[maxLength - 1]) ? maxLength - 1 : maxLength;
        return value[..length];
    }
}
