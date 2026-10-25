using Microsoft.Extensions.Logging;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Security;
using QuanLyChoThuePhongTroWeb.Application.Common.Configurations;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Payments;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Persistence;

namespace QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;

// Đồng bộ bù giao dịch tiền vào qua API SePay khi webhook bị lỡ (spec thanh toán trực tuyến §4.3, §5.2). Chỉ Admin, fail closed.
// Mỗi giao dịch đi qua đúng IBankTransferIngestionService như webhook: cùng bộ phân loại, khóa dòng hóa đơn, quy tắc ghi nhận như Admin
// và thông báo; không có logic tiền riêng ở đây. Chống trùng với webhook nhờ khóa SEPAY-{reference_number}, nên giao dịch thiếu
// reference_number không bao giờ được ghi.
// Khi SePay:SyncRecordEnabled = false (mặc định) chỉ đối chiếu với sổ: không ghi sổ và không thông báo.
// Không log token, nội dung chuyển khoản hay email; log tổng kết chỉ có số lượng theo trạng thái.
public sealed class SePaySyncService(
    ISePayTransactionClient client,
    IBankTransferIngestionService ingestion,
    IEmployeeAccessService access,
    SePayOptions options,
    IInvoicePaymentStore store,
    ILogger<SePaySyncService> logger) : ISePaySyncService
{
    public const int DefaultRangeDays = 3;
    public const int MaxRangeDays = 31;
    public const int MaxPages = 100;

    private const string AdminOnlyMessage = "Chỉ Admin được đồng bộ giao dịch SePay.";
    private const string IncomingType = "in";

    public async Task<ServiceResult<SePaySyncSettingsDto>> GetSettingsAsync(int actorId, CancellationToken ct = default)
    {
        if (!await IsAdminAsync(actorId, ct))
        {
            return ServiceResult<SePaySyncSettingsDto>.Forbidden(AdminOnlyMessage);
        }

        return ServiceResult<SePaySyncSettingsDto>.Ok(new SePaySyncSettingsDto
        {
            DaCauHinhApiToken = !string.IsNullOrWhiteSpace(options.ApiToken),
            SoNgayMacDinh = DefaultRangeDays,
            SoNgayToiDa = MaxRangeDays,
            ChiDoiChieu = !options.SyncRecordEnabled
        });
    }

    public async Task<ServiceResult<SePaySyncResult>> SyncAsync(int actorId, SePaySyncRequest request, CancellationToken ct = default)
    {
        if (!await IsAdminAsync(actorId, ct))
        {
            return ServiceResult<SePaySyncResult>.Forbidden(AdminOnlyMessage);
        }

        if (string.IsNullOrWhiteSpace(options.ApiToken))
        {
            return ServiceResult<SePaySyncResult>.Fail("Chưa cấu hình SePay:ApiToken nên chưa đồng bộ được.");
        }

        if (request == null || request.TuUtc > request.DenUtc)
        {
            return ServiceResult<SePaySyncResult>.Fail("Ngày bắt đầu phải trước hoặc bằng ngày kết thúc.");
        }

        if (request.DenUtc - request.TuUtc >= TimeSpan.FromDays(MaxRangeDays))
        {
            return ServiceResult<SePaySyncResult>.Fail($"Khoảng ngày tối đa {MaxRangeDays} ngày.");
        }

        var rows = new List<SePaySyncRow>();
        // API có thể trả lại cùng một giao dịch khi phân trang bị xô; bỏ qua theo id, không phân biệt hoa thường.
        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var malformed = 0;
        var completed = false;
        string? stopReason = null;

        for (var page = 1; page <= MaxPages; page++)
        {
            var pageResult = await client.GetIncomingPageAsync(request.TuUtc, request.DenUtc, page, ct);
            if (!pageResult.Success)
            {
                stopReason = pageResult.Message;
                break;
            }

            var data = pageResult.Data!;
            malformed += data.SoGiaoDichSaiDinhDang;
            foreach (var item in data.Items)
            {
                if (seenIds.Add(item.Id))
                {
                    rows.Add(await ProcessAsync(item, ct));
                }
            }

            // Trang không có phần tử nào (kể cả phần tử sai định dạng) mà vẫn báo còn trang thì coi là hết, tránh đọc vô ích.
            // Trang chỉ có phần tử sai định dạng thì phải đọc tiếp, nếu không các trang sau bị bỏ sót mà vẫn báo hoàn tất.
            if (!data.HasMore || (data.Items.Count == 0 && data.SoGiaoDichSaiDinhDang == 0))
            {
                completed = true;
                break;
            }

            if (page == MaxPages)
            {
                stopReason = $"Dừng ở trang {MaxPages}: thu hẹp khoảng ngày rồi đồng bộ lại.";
            }
        }

        var totals = Enum.GetValues<SePaySyncRowStatus>()
            .Select(status => new SePaySyncStatusCount(status, Label(status), rows.Count(r => r.TrangThai == status)))
            .ToList();

        logger.LogInformation(
            "Actor {ActorId} đồng bộ SePay từ {TuUtc:o} đến {DenUtc:o}: {Summary}; hoàn tất {HoanTat}; chỉ đối chiếu {ChiDoiChieu}",
            actorId, request.TuUtc, request.DenUtc,
            string.Join(", ", totals.Where(t => t.SoLuong > 0).Select(t => $"{t.TrangThai}={t.SoLuong}")),
            completed, !options.SyncRecordEnabled);

        return ServiceResult<SePaySyncResult>.Ok(new SePaySyncResult
        {
            Rows = rows,
            TongTheoTrangThai = totals,
            HoanTat = completed,
            LoiDongBo = stopReason,
            CanhBao = malformed > 0 ? $"{malformed} giao dịch SePay trả về sai định dạng, không xử lý." : null,
            ChiDoiChieu = !options.SyncRecordEnabled
        });
    }

    private async Task<bool> IsAdminAsync(int actorId, CancellationToken ct)
    {
        if (actorId <= 0)
        {
            return false;
        }

        return (await access.GetScopeAsync(actorId, ct))?.IsAdmin == true;
    }

    private async Task<SePaySyncRow> ProcessAsync(SePayApiTransaction item, CancellationToken ct)
    {
        var maChuyenKhoan = TransferCodeParser.Extract(item.Code, item.Content, null);
        var maThamChieu = string.IsNullOrWhiteSpace(item.ReferenceNumber) ? null : item.ReferenceNumber.Trim();

        // Không bao giờ ghi sổ khi không phải tiền vào hoặc thiếu mã tham chiếu (không chống trùng được với webhook).
        if (!string.Equals(item.TransferType, IncomingType, StringComparison.Ordinal))
        {
            return Row(item, maChuyenKhoan, maThamChieu, SePaySyncRowStatus.BoQua, "Không phải tiền vào");
        }

        if (maThamChieu == null)
        {
            var (hoaDonId, maHoaDon) = await FindInvoiceAsync(maChuyenKhoan, ct);
            return Row(item, maChuyenKhoan, null, SePaySyncRowStatus.ThieuMaThamChieu,
                "Không chống trùng được với webhook. Nếu là tiền hóa đơn, xử lý bằng Thu thủ công. Kiểm lịch sử thanh toán của hóa đơn trước khi Thu thủ công.",
                hoaDonId, maHoaDon);
        }

        // SePayId = 0 và có ReferenceCode nên DedupKey luôn là SEPAY-{mã}, cùng khóa với webhook; không bao giờ là SEPAY-ID-0.
        var transfer = new IncomingBankTransfer(
            SePayId: 0,
            Gateway: item.BankBrandName,
            TransactionDateUtc: item.TransactionDateUtc,
            AccountNumber: item.AccountNumber,
            SubAccount: item.SubAccount,
            Code: item.Code,
            Content: item.Content,
            TransferType: item.TransferType,
            TransferAmount: item.AmountIn,
            ReferenceCode: maThamChieu,
            Description: null)
        {
            LaDongBoBu = true
        };

        // Chưa kiểm referenceCode (webhook) = reference_number (API): chỉ đối chiếu, tuyệt đối không ghi sổ.
        if (!options.SyncRecordEnabled)
        {
            if (await store.ExistsMaGiaoDichIncludingDeletedAsync(transfer.DedupKey, ct))
            {
                return Row(item, maChuyenKhoan, maThamChieu, SePaySyncRowStatus.DaCo, null);
            }

            // Dòng chưa ghi sổ cần Admin xử lý tay nên kèm link tới hóa đơn mà mã chuyển khoản trỏ tới.
            var (invoiceId, invoiceCode) = await FindInvoiceAsync(maChuyenKhoan, ct);
            return Row(item, maChuyenKhoan, maThamChieu, SePaySyncRowStatus.ChuaGhiSo,
                "Chế độ chỉ đối chiếu (SePay:SyncRecordEnabled = false). Kiểm lịch sử thanh toán của hóa đơn trước khi Thu thủ công.",
                invoiceId, invoiceCode);
        }

        var outcome = await ingestion.IngestAsync(transfer, ct);
        return FromOutcome(item, maChuyenKhoan, maThamChieu, outcome);
    }

    // Chỉ để Admin bấm sang hóa đơn: mã chuyển khoản là dữ liệu ngoài nên chỉ nhận khi đúng dạng TTPTHD{Id} và hóa đơn có thật.
    private async Task<(int? HoaDonId, string? MaHoaDon)> FindInvoiceAsync(string? maChuyenKhoan, CancellationToken ct)
    {
        var hoaDonId = TransferCodeParser.ExtractInvoiceId(maChuyenKhoan);
        if (hoaDonId == null)
        {
            return (null, null);
        }

        var invoice = await store.GetInvoiceSnapshotAsync(hoaDonId.Value, ct);
        return invoice == null ? (null, null) : (invoice.HoaDonId, invoice.MaHoaDon);
    }

    private static SePaySyncRow FromOutcome(SePayApiTransaction item, string? maChuyenKhoan, string? maThamChieu, TransferOutcome outcome)
    {
        (SePaySyncRowStatus Status, string? Note) mapped = outcome.Kind switch
        {
            TransferOutcomeKind.Recorded => (SePaySyncRowStatus.MoiGhiNhan, RecordedNote(outcome)),
            TransferOutcomeKind.Duplicate => (SePaySyncRowStatus.DaCo, null),
            TransferOutcomeKind.Unmatched => (SePaySyncRowStatus.KhongKhopMa, null),
            TransferOutcomeKind.InvoiceNotPayable => (SePaySyncRowStatus.HoaDonKhongNhanTien, outcome.LyDo),
            TransferOutcomeKind.AlreadyPaid => (SePaySyncRowStatus.HoaDonKhongNhanTien, "Hóa đơn đã thu đủ"),
            TransferOutcomeKind.Ignored => (SePaySyncRowStatus.BoQua, "Số tiền không hợp lệ"),
            TransferOutcomeKind.WrongAccount => (SePaySyncRowStatus.BoQua, "Tiền vào tài khoản khác"),
            // Failed (và loại kết quả mới chưa biết): coi là lỗi ghi sổ để Admin đồng bộ lại.
            _ => (SePaySyncRowStatus.LoiGhiSo, "Lỗi hệ thống, bấm Đồng bộ lại")
        };

        return Row(item, maChuyenKhoan, maThamChieu, mapped.Status, mapped.Note, outcome.HoaDonId, outcome.MaHoaDon);
    }

    // "Ghi 1.500.000đ, còn lại 500.000đ" và ", tiền thừa 100.000đ" khi khách chuyển dư.
    private static string RecordedNote(TransferOutcome outcome)
    {
        var note = $"Ghi {InvoicePaymentPolicy.FormatVnd(outcome.SoTienGhiNhan)}đ, còn lại {InvoicePaymentPolicy.FormatVnd(outcome.ConLai)}đ";
        return outcome.TienThua > 0
            ? note + $", tiền thừa {InvoicePaymentPolicy.FormatVnd(outcome.TienThua)}đ"
            : note;
    }

    private static SePaySyncRow Row(
        SePayApiTransaction item, string? maChuyenKhoan, string? maThamChieu, SePaySyncRowStatus status, string? note,
        int? hoaDonId = null, string? maHoaDon = null) => new()
    {
        SePayApiId = item.Id,
        NgayGiaoDichUtc = item.TransactionDateUtc,
        SoTien = item.AmountIn,
        NoiDung = item.Content,
        MaThamChieu = maThamChieu,
        MaChuyenKhoan = maChuyenKhoan,
        HoaDonId = hoaDonId,
        MaHoaDon = maHoaDon,
        TrangThai = status,
        NhanTrangThai = Label(status),
        GhiChu = note
    };

    internal static string Label(SePaySyncRowStatus status) => status switch
    {
        SePaySyncRowStatus.MoiGhiNhan => "Mới ghi nhận",
        SePaySyncRowStatus.DaCo => "Đã có",
        SePaySyncRowStatus.KhongKhopMa => "Không khớp mã",
        SePaySyncRowStatus.HoaDonKhongNhanTien => "Hóa đơn không nhận tiền",
        SePaySyncRowStatus.ThieuMaThamChieu => "Thiếu mã tham chiếu",
        SePaySyncRowStatus.BoQua => "Bỏ qua",
        SePaySyncRowStatus.LoiGhiSo => "Lỗi ghi sổ",
        SePaySyncRowStatus.ChuaGhiSo => "Chưa ghi sổ",
        _ => status.ToString()
    };
}
