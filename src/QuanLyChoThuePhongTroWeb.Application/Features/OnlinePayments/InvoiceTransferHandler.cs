using System.Globalization;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.AutoInvoices;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Payments;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Services;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;

// Ghi sổ hóa đơn khi có tiền vào với mã TTPTHD{HoaDonId} (spec thanh toán trực tuyến §5).
// Không có actorId và không kiểm phân quyền chi nhánh: chỉ được gọi từ IBankTransferIngestionService,
// nơi webhook đã xác thực chữ ký SePay hoặc đồng bộ bù đã kiểm Admin. Hệ thống được xử lý như Admin
// (ghi đủ nợ, phần dư gắn thẻ tiền thừa) và không xét cấu hình trả một phần, giống thu thủ công.
public sealed class InvoiceTransferHandler(InvoicePaymentTransaction transaction) : IIncomingTransferHandler
{
    public const string InvoicePrefix = "TTPTHD";

    private const int MaxIdDigits = 9;
    // ThongBao.TieuDe tối đa 255 ký tự.
    private const int MaxTitleLength = 255;
    private const string InvoiceNotFoundReason = "Không tìm thấy hóa đơn";

    public string Prefix => InvoicePrefix;

    // Mã chuyển khoản của một hóa đơn (TTPTHD{HoaDonId}), dùng chung cho QR, email và PDF để mọi nơi khách quét đều khớp với bộ phân loại.
    public static string TransferCode(int hoaDonId) => InvoicePrefix + hoaDonId.ToString(CultureInfo.InvariantCulture);

    public async Task<TransferOutcome> HandleAsync(IncomingBankTransfer transfer, string code, CancellationToken ct = default)
    {
        // Bộ phân loại đã loại số tiền không dương; kiểm lại để không bao giờ ghi sổ khoản 0đ hoặc âm.
        if (transfer.TransferAmount <= 0)
        {
            return new TransferOutcome { Kind = TransferOutcomeKind.Ignored, Transfer = transfer, Code = code };
        }

        if (!TryReadInvoiceId(code, out var hoaDonId))
        {
            return new TransferOutcome { Kind = TransferOutcomeKind.Unmatched, Transfer = transfer, Code = code };
        }

        var result = await transaction.RunAsync(hoaDonId, actorId: null, scope => RecordAsync(scope, transfer, code, ct), ct);
        if (result.Success)
        {
            return result.Data!;
        }

        if (result.ErrorKind == ServiceErrorKind.NotFound)
        {
            return new TransferOutcome
            {
                Kind = TransferOutcomeKind.InvoiceNotPayable,
                Transfer = transfer,
                Code = code,
                HoaDonId = hoaDonId,
                LyDo = InvoiceNotFoundReason
            };
        }

        // Vi phạm unique index MaGiaoDich khi lưu: giao dịch này đã được ghi bởi một lần xử lý khác.
        if (result.Message == InvoicePaymentTransaction.MaGiaoDichDaGhiNhanMessage)
        {
            return new TransferOutcome { Kind = TransferOutcomeKind.Duplicate, Transfer = transfer, Code = code, HoaDonId = hoaDonId };
        }

        return new TransferOutcome
        {
            Kind = TransferOutcomeKind.Failed,
            Transfer = transfer,
            Code = code,
            HoaDonId = hoaDonId,
            LyDo = result.Message
        };
    }

    // Chạy dưới khóa dòng hoa_don, theo đúng thứ tự spec §5.1. Mọi kết quả nghiệp vụ trả Ok để khung commit
    // (ngoài phần HetHan khung tự ghi, các nhánh không ghi sổ không có gì để lưu).
    private static async Task<ServiceResult<TransferOutcome>> RecordAsync(
        InvoicePaymentScope scope, IncomingBankTransfer transfer, string code, CancellationToken ct)
    {
        var hoaDon = scope.HoaDon;
        TransferOutcome Outcome(TransferOutcomeKind kind) => new()
        {
            Kind = kind,
            Transfer = transfer,
            Code = code,
            HoaDonId = hoaDon.HoaDonId,
            MaHoaDon = hoaDon.MaHoaDon
        };

        // 1. Đã xử lý? Tính cả dòng đã hủy ghi nhận để lần gửi lại không ghi lại khoản Admin đã hủy.
        if (await scope.Store.ExistsMaGiaoDichForInvoiceIncludingDeletedAsync(hoaDon.HoaDonId, transfer.DedupKey, ct))
        {
            return ServiceResult<TransferOutcome>.Ok(Outcome(TransferOutcomeKind.Duplicate));
        }

        // 2. Hóa đơn nhận tiền được?
        var notPayableReason = NotPayableReason(hoaDon);
        if (notPayableReason != null)
        {
            return ServiceResult<TransferOutcome>.Ok(Outcome(TransferOutcomeKind.InvoiceNotPayable) with { LyDo = notPayableReason });
        }

        // 3. Đã trả đủ?
        if (scope.ConLai <= 0)
        {
            return ServiceResult<TransferOutcome>.Ok(Outcome(TransferOutcomeKind.AlreadyPaid) with
            {
                TongTien = hoaDon.TongTien,
                DaThu = scope.DaThu,
                ConLai = scope.ConLai
            });
        }

        // 4. Ghi nhận như Admin: không vượt số còn lại, phần dư (nếu có) chỉ đánh dấu.
        var decision = InvoicePaymentPolicy.DecideRecording(transfer.TransferAmount, null, scope.ConLai, isAdmin: true);

        // 5. Lượt cũ: lượt chờ thanh toán không còn đúng số tiền nên hủy; lượt khách đã nộp minh chứng giữ nguyên
        // để nhân viên đối chiếu, nhưng báo lại vì xác nhận lượt đó sẽ ghi trùng khoản vừa nhận.
        string? maLuotDaHuy = null;
        var conMinhChungChoDoiChieu = false;
        var active = await scope.Store.GetActiveRequestAsync(hoaDon.HoaDonId, scope.NowUtc, ct);
        if (active?.TrangThai == TrangThaiYeuCauThanhToan.ChoThanhToan)
        {
            active.Huy(null, scope.NowUtc, "Đã nhận chuyển khoản tự động");
            maLuotDaHuy = active.MaYeuCau;
        }
        else if (active?.TrangThai == TrangThaiYeuCauThanhToan.DangDoiChieu)
        {
            conMinhChungChoDoiChieu = true;
        }

        // 6-7. Dòng sổ mới.
        var ledger = new LichSuThanhToan
        {
            HoaDonId = hoaDon.HoaDonId,
            MaGiaoDich = transfer.DedupKey,
            SoTienThanhToan = decision.SoTienGhiNhan,
            PhuongThucThanhToan = PhuongThucThanhToan.ChuyenKhoan,
            NgayThanhToan = transfer.TransactionDateUtc,
            NgayXacNhan = scope.NowUtc,
            NguoiXacNhanId = null,
            GhiChu = BuildNote(transfer, decision)
        };
        scope.Store.AddLedgerEntry(ledger);

        // 8. Lưu, đọc lại tổng đã thu và tính lại trạng thái hóa đơn.
        var lyDo = AutoInvoiceHistoryTags.TaoLyDo(
            AutoInvoiceHistoryTags.TuDong,
            $"SePay ghi nhận {InvoicePaymentPolicy.FormatVnd(decision.SoTienGhiNhan)}đ, {Reference(transfer)}");
        await scope.ApplyLedgerChangeAsync(Truncate(lyDo, InvoicePaymentPolicy.MaxLyDoLength));

        // Thông báo trong app (spec §6.4): tạo trong cùng transaction, khung đẩy SignalR sau commit.
        // Telegram, email biên lai và sự kiện cho panel của khách do IPaymentOutcomePublisher gửi sau khi có kết quả.
        var tenantUserId = await NotifyRecordedAsync(scope, transfer, decision, conMinhChungChoDoiChieu, ct);

        return ServiceResult<TransferOutcome>.Ok(Outcome(TransferOutcomeKind.Recorded) with
        {
            LichSuThanhToanId = ledger.LichSuThanhToanId,
            TenantUserId = tenantUserId,
            SoTienGhiNhan = decision.SoTienGhiNhan,
            TienThua = decision.Kind == RecordingKind.Overpay ? decision.ChenhLech : 0,
            TongTien = hoaDon.TongTien,
            DaThu = scope.DaThu,
            ConLai = scope.ConLai,
            TrangThaiThanhToan = InvoiceStatusLabels.ThanhToan(hoaDon.TrangThaiHoaDon),
            MaLuotDaHuy = maLuotDaHuy,
            ConMinhChungChoDoiChieu = conMinhChungChoDoiChieu
        });
    }

    // Báo Admin, nhân viên chi nhánh của phòng và khách đứng tên hợp đồng. Câu về tiền thừa chỉ Admin thấy.
    // Trả tài khoản khách để phát sự kiện cho panel thanh toán sau commit; null nếu khách chưa có tài khoản hoạt động.
    private static async Task<int?> NotifyRecordedAsync(
        InvoicePaymentScope scope, IncomingBankTransfer transfer, RecordingDecision decision, bool conMinhChungChoDoiChieu, CancellationToken ct)
    {
        var hoaDon = scope.HoaDon;
        var tenantUserId = await scope.Store.GetTenantUserIdAsync(hoaDon.HopDongId, ct);
        var target = await scope.Store.GetTargetByHoaDonAsync(hoaDon.HoaDonId, ct);
        var invoice = await scope.Store.GetInvoiceSnapshotAsync(hoaDon.HoaDonId, ct);
        var admins = (await scope.Store.GetAdminUserIdsAsync(ct)).ToHashSet();
        IEnumerable<int> reviewers = target != null
            ? await scope.Store.GetReviewerUserIdsAsync(target.ChiNhanhId, ct)
            : admins;

        var tieuDe = Truncate(
            $"Đã nhận chuyển khoản {Vnd(transfer.TransferAmount)}đ — {invoice?.TenPhong ?? string.Empty}", MaxTitleLength);

        var noiDungNhanVien =
            $"Hóa đơn {hoaDon.MaHoaDon}: ghi nhận {Vnd(decision.SoTienGhiNhan)}đ qua chuyển khoản tự động ({Reference(transfer)}), " +
            $"đã thu {Vnd(scope.DaThu)}/{Vnd(hoaDon.TongTien)}đ, còn lại {Vnd(scope.ConLai)}đ.";
        if (conMinhChungChoDoiChieu)
        {
            noiDungNhanVien += " Hóa đơn còn minh chứng chờ đối chiếu, kiểm tra trùng trước khi xác nhận.";
        }

        var noiDungAdmin = decision.Kind == RecordingKind.Overpay
            ? noiDungNhanVien + $" Khách chuyển dư {Vnd(decision.ChenhLech)}đ, cần xử lý ngoài hệ thống."
            : noiDungNhanVien;

        var linkQuanLy = $"/QuanLyNhaTro/QuanLyHoaDon?hoaDonId={hoaDon.HoaDonId}";
        foreach (var userId in reviewers)
        {
            scope.Notify(userId, tieuDe, admins.Contains(userId) ? noiDungAdmin : noiDungNhanVien, linkQuanLy);
        }

        if (tenantUserId.HasValue)
        {
            scope.Notify(
                tenantUserId.Value,
                tieuDe,
                $"Hóa đơn {hoaDon.MaHoaDon}: đã ghi nhận {Vnd(decision.SoTienGhiNhan)}đ, còn lại {Vnd(scope.ConLai)}đ.",
                $"/KhachThue/HoaDon?hoaDonId={hoaDon.HoaDonId}");
        }

        return tenantUserId;
    }

    private static string? NotPayableReason(HoaDon hoaDon)
    {
        if (hoaDon.IsDeleted || hoaDon.TrangThaiPhatHanh == TrangThaiPhatHanhHoaDon.DaHuy)
        {
            return "Hóa đơn đã hủy";
        }

        if (hoaDon.TrangThaiPhatHanh != TrangThaiPhatHanhHoaDon.DaGui)
        {
            return "Hóa đơn chưa công bố";
        }

        return hoaDon.TongTienLaSoNguyen() ? null : "Hóa đơn có số tiền lẻ";
    }

    // Phần sau tiền tố phải là 1-9 chữ số ASCII và là số nguyên dương.
    private static bool TryReadInvoiceId(string code, out int hoaDonId)
    {
        hoaDonId = 0;
        if (!code.StartsWith(InvoicePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var digits = code.AsSpan(InvoicePrefix.Length);
        if (digits.Length is < 1 or > MaxIdDigits)
        {
            return false;
        }

        foreach (var c in digits)
        {
            if (c is < '0' or > '9')
            {
                return false;
            }
        }

        return int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out hoaDonId) && hoaDonId > 0;
    }

    // Thẻ [TIEN-THUA:n] đặt đầu chuỗi nên không bị cắt. Nội dung từ ngân hàng đã bỏ [ ] nên không giả được thẻ hệ thống.
    private static string BuildNote(IncomingBankTransfer transfer, RecordingDecision decision)
    {
        var note = $"[SEPAY] {Clean(transfer.Gateway)} · {Reference(transfer)} · ND: {Clean(transfer.Content)}";
        if (decision.Kind == RecordingKind.Overpay)
        {
            note = PaymentNoteTags.TienThua(decision.ChenhLech, note);
        }

        return Truncate(note, InvoicePaymentPolicy.MaxLyDoLength);
    }

    private static string Vnd(decimal soTien) => InvoicePaymentPolicy.FormatVnd(soTien);

    // Mã tham chiếu ngân hàng; giao dịch không có thì dùng id SePay.
    private static string Reference(IncomingBankTransfer transfer)
    {
        var reference = Clean(transfer.ReferenceCode);
        return reference.Length == 0 ? $"ID {transfer.SePayId}" : $"REF {reference}";
    }

    // Chuỗi lấy từ ngân hàng/SePay ghi vào ghi chú và lý do lịch sử: bỏ [ ] vì PaymentNoteTags nhận diện thẻ ở mọi vị trí.
    private static string Clean(string? value)
    {
        return (value ?? string.Empty).Replace("[", string.Empty).Replace("]", string.Empty).Trim();
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
