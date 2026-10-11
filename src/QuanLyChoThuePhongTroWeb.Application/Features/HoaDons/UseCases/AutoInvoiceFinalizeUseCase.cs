using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Services;
using QuanLyChoThuePhongTroWeb.Application.Common.Configurations;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.AutoInvoices;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Services;
using QuanLyChoThuePhongTroWeb.Application.Features.ThongBaos.Services;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.UseCases
{
    // Tự chốt và công bố hóa đơn tạm tính khi hết hạn phản hồi. Không kiểm quyền: đây là ngoại lệ có chủ đích
    // của quy tắc "chỉ Admin chốt hóa đơn" (spec hóa đơn tự động mục 5.3).
    public class AutoInvoiceFinalizeUseCase : IAutoInvoiceFinalizeUseCase
    {
        private readonly IAutoInvoiceStore _autoStore;
        private readonly IInvoiceIssuanceStore _issuanceStore;
        private readonly IInvoicePublicationStore _publicationStore;
        private readonly IHoaDonStore _hoaDonStore;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IThongBaoService _thongBaoService;
        private readonly IEmailService _emailService;
        private readonly IInvoiceDocumentExporter _documentExporter;
        private readonly IVietQRService _vietQRService;
        private readonly VietQrSettings _vietQrSettings;
        private readonly InvoiceIssuanceOptions _issuanceOptions;
        private readonly AutoInvoiceFinalizeNotifyGuard _guard;
        private readonly ILogger<AutoInvoiceFinalizeUseCase> _logger;
        private readonly TimeProvider _time;

        public AutoInvoiceFinalizeUseCase(
            IAutoInvoiceStore autoStore,
            IInvoiceIssuanceStore issuanceStore,
            IInvoicePublicationStore publicationStore,
            IHoaDonStore hoaDonStore,
            IUnitOfWork unitOfWork,
            IThongBaoService thongBaoService,
            IEmailService emailService,
            IInvoiceDocumentExporter documentExporter,
            IVietQRService vietQRService,
            VietQrSettings vietQrSettings,
            InvoiceIssuanceOptions issuanceOptions,
            AutoInvoiceFinalizeNotifyGuard guard,
            ILogger<AutoInvoiceFinalizeUseCase> logger,
            TimeProvider? timeProvider = null)
        {
            _autoStore = autoStore ?? throw new ArgumentNullException(nameof(autoStore));
            _issuanceStore = issuanceStore ?? throw new ArgumentNullException(nameof(issuanceStore));
            _publicationStore = publicationStore ?? throw new ArgumentNullException(nameof(publicationStore));
            _hoaDonStore = hoaDonStore ?? throw new ArgumentNullException(nameof(hoaDonStore));
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _thongBaoService = thongBaoService ?? throw new ArgumentNullException(nameof(thongBaoService));
            _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
            _documentExporter = documentExporter ?? throw new ArgumentNullException(nameof(documentExporter));
            _vietQRService = vietQRService ?? throw new ArgumentNullException(nameof(vietQRService));
            _vietQrSettings = vietQrSettings ?? throw new ArgumentNullException(nameof(vietQrSettings));
            _issuanceOptions = issuanceOptions ?? throw new ArgumentNullException(nameof(issuanceOptions));
            _guard = guard ?? throw new ArgumentNullException(nameof(guard));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _time = timeProvider ?? TimeProvider.System;
        }

        public async Task<int> ExecuteAsync(CancellationToken cancellationToken = default)
        {
            _autoStore.DiscardPendingChanges();
            var now = _time.GetUtcNow().UtcDateTime;
            var due = await _autoStore.GetDueProvisionalInvoicesAsync(now, cancellationToken);
            if (due.Count == 0)
            {
                return 0;
            }

            _logger.LogInformation("Tìm thấy {Count} hóa đơn tạm tính đến hạn tự chốt.", due.Count);

            var finalized = 0;
            foreach (var item in due)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                try
                {
                    if (await FinalizeOneAsync(item, cancellationToken))
                    {
                        finalized++;
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    // FinalizeOneAsync đã tự xử lý lỗi của từng hóa đơn; đây chỉ là lưới an toàn cho vòng lặp.
                    _autoStore.DiscardPendingChanges();
                    _logger.LogWarning(ex, "Lỗi ngoài dự kiến khi xử lý hóa đơn {HoaDonId} trong vòng tự chốt.", item.HoaDonId);
                }
            }

            return finalized;
        }

        private async Task<bool> FinalizeOneAsync(AutoInvoiceDueItem item, CancellationToken ct)
        {
            _autoStore.DiscardPendingChanges();
            var now = _time.GetUtcNow().UtcDateTime;

            var targets = await _issuanceStore.GetInvoiceLockTargetsAsync(item.HoaDonId, ct);
            if (targets == null)
            {
                _logger.LogInformation("Bỏ qua tự chốt hóa đơn {HoaDonId}: không còn hóa đơn.", item.HoaDonId);
                return false;
            }

            var (meterPeriodId, _) = targets.Value;
            string? lyDoLoi = null;
            string? lyDoImLang = null;
            HoaDon? hoaDon = null;

            await using var tx = await _unitOfWork.BeginTransactionAsync(ct);
            try
            {
                TrangThaiGhiNhan? periodStatus = null;
                if (meterPeriodId.HasValue)
                {
                    periodStatus = await _issuanceStore.LockMeterPeriodAsync(meterPeriodId.Value, ct);
                }

                var locked = await _issuanceStore.GetInvoiceForUpdateAsync(item.HoaDonId, ct);
                if (locked == null
                    || locked.TrangThaiPhatHanh != TrangThaiPhatHanhHoaDon.ChoDuyet
                    || locked.HanPhanHoi == null
                    || locked.HanPhanHoi > now)
                {
                    lyDoImLang = "hóa đơn không còn chờ tự chốt hoặc hạn đã bị dời";
                }
                else if (!meterPeriodId.HasValue || periodStatus != TrangThaiGhiNhan.DaDuyet)
                {
                    lyDoLoi = "Kỳ chỉ số điện nước liên kết không còn ở trạng thái đã chốt";
                }
                else
                {
                    var activeDetails = locked.ChiTietHoaDonDichVus.Where(x => !x.IsDeleted).ToList();
                    if (activeDetails.Count == 0 || locked.TongTien <= 0 || locked.TongTien != activeDetails.Sum(x => x.TongTien))
                    {
                        lyDoLoi = "Tổng tiền hóa đơn không khớp với tổng chi tiết dịch vụ";
                    }
                    else if (await _issuanceStore.HasOtherActiveInvoiceAsync(locked.HopDongId, locked.Thang, locked.Nam, locked.HoaDonId, ct))
                    {
                        lyDoLoi = "Đã tồn tại hóa đơn đang hoạt động khác cho hợp đồng trong kỳ này";
                    }
                }

                int? actor = null;
                if (lyDoImLang == null && lyDoLoi == null)
                {
                    actor = await _autoStore.GetLastProvisionalSubmitterIdAsync(item.HoaDonId, ct);
                    if (actor == null || actor <= 0)
                    {
                        lyDoLoi = "Không xác định được người gửi hóa đơn tạm tính";
                    }
                }

                if (lyDoImLang != null || lyDoLoi != null)
                {
                    await TryRollbackAsync(tx, item.HoaDonId);
                    _autoStore.DiscardPendingChanges();
                    if (lyDoImLang != null)
                    {
                        _logger.LogInformation("Bỏ qua tự chốt hóa đơn {HoaDonId}: {LyDo}.", item.HoaDonId, lyDoImLang);
                    }
                    else
                    {
                        _logger.LogWarning("Chưa tự chốt được hóa đơn {HoaDonId}: {LyDo}.", item.HoaDonId, lyDoLoi);
                        await BaoNhanVienAsync(item, lyDoLoi!);
                    }

                    return false;
                }

                hoaDon = locked!;
                hoaDon.ChotHoaDon(actor!.Value, AutoInvoiceHistoryTags.TaoLyDo(AutoInvoiceHistoryTags.TuDong, "Tự chốt khi hết hạn phản hồi"));
                // Domain ghi mốc chốt bằng đồng hồ của nó; đọc lại đồng hồ ở đây để mốc gửi (và hạn thanh toán) không sớm hơn mốc chốt.
                var guiLuc = _time.GetUtcNow().UtcDateTime;
                hoaDon.GuiHoaDon(
                    actor.Value,
                    guiLuc,
                    InvoiceDueDateCalculator.Compute(guiLuc, _issuanceOptions.SoNgayHanThanhToan),
                    AutoInvoiceHistoryTags.TaoLyDo(AutoInvoiceHistoryTags.TuDong, "Tự công bố hóa đơn lên cổng khách thuê"));
                await _unitOfWork.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                _guard.Forget(item.HoaDonId);
            }
            catch (OperationCanceledException)
            {
                await TryRollbackAsync(tx, item.HoaDonId);
                _autoStore.DiscardPendingChanges();
                throw;
            }
            catch (Exception ex)
            {
                await TryRollbackAsync(tx, item.HoaDonId);
                _autoStore.DiscardPendingChanges();
                _logger.LogError(ex, "Lỗi khi tự chốt hóa đơn {HoaDonId}.", item.HoaDonId);
                await BaoNhanVienAsync(item, "Lỗi hệ thống khi tự chốt");
                return false;
            }

            var hopDongId = hoaDon.HopDongId;
            var maHoaDon = hoaDon.MaHoaDon;
            var thang = hoaDon.Thang;
            var nam = hoaDon.Nam;
            var tongTien = hoaDon.TongTien;
            var hanThanhToan = hoaDon.HanThanhToan;
            _autoStore.DiscardPendingChanges();

            await BaoKhachAsync(item.HoaDonId, hopDongId, maHoaDon, thang, nam, tongTien, hanThanhToan);

            _logger.LogInformation("Đã tự chốt hóa đơn {HoaDonId}.", item.HoaDonId);
            return true;
        }

        private async Task BaoKhachAsync(int hoaDonId, int hopDongId, string maHoaDon, int thang, int nam, decimal tongTien, DateTime? hanThanhToan)
        {
            InvoiceRecipientInfo? recipient = null;
            try
            {
                recipient = await _publicationStore.GetRecipientAsync(hopDongId, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không đọc được người nhận để báo khách về hóa đơn tự chốt {HoaDonId}.", hoaDonId);
            }

            if (recipient == null)
            {
                _logger.LogWarning("Không tìm thấy người nhận để báo khách về hóa đơn tự chốt {HoaDonId}.", hoaDonId);
                return;
            }

            try
            {
                var hanText = hanThanhToan.HasValue
                    ? hanThanhToan.Value.AddHours(7).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)
                    : string.Empty;
                var noiDung = $"Hóa đơn {maHoaDon} tháng {thang}/{nam}: tổng tiền {tongTien.ToString("#,##0", CultureInfo.InvariantCulture)} đ, hạn thanh toán hết ngày {hanText}.";
                var sent = await _thongBaoService.GuiChoKhachThueAsync(
                    recipient.NguoiThueId,
                    $"Hóa đơn tháng {thang}/{nam}",
                    noiDung,
                    "HoaDon",
                    $"/KhachThue/HoaDon?hoaDonId={hoaDonId}");
                _autoStore.DiscardPendingChanges();
                if (!sent.Success)
                {
                    _logger.LogWarning("Không gửi được thông báo cổng về hóa đơn tự chốt {HoaDonId}: {Message}", hoaDonId, sent.Message);
                }
            }
            catch (Exception ex)
            {
                _autoStore.DiscardPendingChanges();
                _logger.LogWarning(ex, "Lỗi khi gửi thông báo cổng về hóa đơn tự chốt {HoaDonId}.", hoaDonId);
            }

            if (string.IsNullOrWhiteSpace(recipient.Email))
            {
                return;
            }

            try
            {
                var detail = await _hoaDonStore.GetHoaDonDetailByIdAsync(hoaDonId, CancellationToken.None);
                if (detail == null)
                {
                    _logger.LogWarning("Không đọc được hóa đơn tự chốt {HoaDonId} để gửi email.", hoaDonId);
                    return;
                }

                var pdf = InvoicePdfComposer.Compose(detail, _vietQRService, _vietQrSettings, _documentExporter);
                if (pdf == null || pdf.Length == 0)
                {
                    _logger.LogWarning("Không tạo được PDF cho hóa đơn tự chốt {HoaDonId}.", hoaDonId);
                    return;
                }

                var (ok, err) = await _emailService.SendInvoiceEmailAsync(recipient.Email, detail, pdf);
                if (!ok)
                {
                    _logger.LogWarning("Không gửi được email hóa đơn tự chốt {HoaDonId}: {Error}", hoaDonId, err);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Lỗi khi gửi email hóa đơn tự chốt {HoaDonId}.", hoaDonId);
            }
        }

        private async Task BaoNhanVienAsync(AutoInvoiceDueItem item, string lyDo)
        {
            if (!_guard.ShouldNotify(item.HoaDonId, lyDo))
            {
                return;
            }

            try
            {
                _autoStore.DiscardPendingChanges();
                var sent = await _thongBaoService.GuiChoNguoiPhuTrachPhongAsync(
                    item.PhongTroId,
                    $"Hóa đơn {item.MaHoaDon}: chưa tự chốt được",
                    $"Lý do: {lyDo}. Hóa đơn vẫn ở trạng thái tạm tính, vui lòng kiểm tra và dùng Điều chỉnh và gửi lại.",
                    "HoaDon",
                    "/QuanLyNhaTro/QuanLyHoaDon");
                _autoStore.DiscardPendingChanges();
                if (!sent.Success)
                {
                    _logger.LogWarning("Không gửi được thông báo cho nhân viên về hóa đơn {HoaDonId}: {Message}", item.HoaDonId, sent.Message);
                }
            }
            catch (Exception ex)
            {
                _autoStore.DiscardPendingChanges();
                _logger.LogWarning(ex, "Lỗi khi gửi thông báo cho nhân viên về hóa đơn {HoaDonId}.", item.HoaDonId);
            }
        }

        private async Task TryRollbackAsync(IApplicationTransaction tx, int hoaDonId)
        {
            try
            {
                // Rollback phải chạy cả khi tác vụ bị hủy, nên không dùng token của tác vụ.
                await tx.RollbackAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không thể rollback transaction tự chốt hóa đơn {HoaDonId}", hoaDonId);
            }
        }
    }
}
