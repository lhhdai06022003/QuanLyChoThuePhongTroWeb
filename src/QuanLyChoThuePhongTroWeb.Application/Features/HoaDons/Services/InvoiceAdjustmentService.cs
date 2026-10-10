using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Security;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Services;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Common.Security;
using QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.Services;
using QuanLyChoThuePhongTroWeb.Application.Features.Emails.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.AutoInvoices;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Features.ThongBaos.Services;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Services
{
    // "Điều chỉnh và gửi lại" hóa đơn tự động theo yêu cầu khách: đổi chỉ số kỳ, tính lại và gửi lại bản tạm tính với hạn mới.
    public class InvoiceAdjustmentService : IInvoiceAdjustmentService
    {
        private const string LyDoDieuChinh = "Điều chỉnh hóa đơn theo yêu cầu khách thuê";
        private const string KhongPhaiTuDong = "Chỉ hóa đơn tự động mới dùng được Điều chỉnh và gửi lại.";
        private const string TrangThaiKhongHopLe = "Hóa đơn không ở trạng thái điều chỉnh được.";
        private const string ThieuQuyen = "Bạn không có quyền điều chỉnh hóa đơn tại chi nhánh này.";
        private const string KhongCoKy = "Hóa đơn không gắn kỳ chỉ số điện nước.";
        private const string KySauDaChot = "Kỳ sau đã được chốt hoặc đã có hóa đơn phát hành, không thể điều chỉnh.";
        private const string DaCoKhoanThu = "Hóa đơn đã có khoản thu, không thể điều chỉnh.";
        private const string DangDoiChieu = "Khách đã nộp minh chứng, cần đối chiếu trước khi điều chỉnh.";
        private const string TongTienLech = "Tổng tiền hóa đơn không khớp với tổng chi tiết dịch vụ.";

        private readonly IAutoInvoiceStore _autoStore;
        private readonly IInvoiceIssuanceStore _issuanceStore;
        private readonly IInvoiceIssuanceService _issuanceService;
        private readonly IMeterReadingWorkflowService _meterWorkflow;
        private readonly IMeterImageStore _meterStore;
        private readonly IInvoicePaymentStore _paymentStore;
        private readonly IInvoicePublicationStore _publicationStore;
        private readonly IHoaDonStore _hoaDonStore;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IEmployeeAccessService _employeeAccessService;
        private readonly IThongBaoService _thongBaoService;
        private readonly IEmailService _emailService;
        private readonly ILogger<InvoiceAdjustmentService> _logger;
        private readonly TimeProvider _time;

        public InvoiceAdjustmentService(
            IAutoInvoiceStore autoStore,
            IInvoiceIssuanceStore issuanceStore,
            IInvoiceIssuanceService issuanceService,
            IMeterReadingWorkflowService meterWorkflow,
            IMeterImageStore meterStore,
            IInvoicePaymentStore paymentStore,
            IInvoicePublicationStore publicationStore,
            IHoaDonStore hoaDonStore,
            IUnitOfWork unitOfWork,
            IEmployeeAccessService employeeAccessService,
            IThongBaoService thongBaoService,
            IEmailService emailService,
            ILogger<InvoiceAdjustmentService> logger,
            TimeProvider? timeProvider = null)
        {
            _autoStore = autoStore ?? throw new ArgumentNullException(nameof(autoStore));
            _issuanceStore = issuanceStore ?? throw new ArgumentNullException(nameof(issuanceStore));
            _issuanceService = issuanceService ?? throw new ArgumentNullException(nameof(issuanceService));
            _meterWorkflow = meterWorkflow ?? throw new ArgumentNullException(nameof(meterWorkflow));
            _meterStore = meterStore ?? throw new ArgumentNullException(nameof(meterStore));
            _paymentStore = paymentStore ?? throw new ArgumentNullException(nameof(paymentStore));
            _publicationStore = publicationStore ?? throw new ArgumentNullException(nameof(publicationStore));
            _hoaDonStore = hoaDonStore ?? throw new ArgumentNullException(nameof(hoaDonStore));
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _employeeAccessService = employeeAccessService ?? throw new ArgumentNullException(nameof(employeeAccessService));
            _thongBaoService = thongBaoService ?? throw new ArgumentNullException(nameof(thongBaoService));
            _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _time = timeProvider ?? TimeProvider.System;
        }

        public async Task<ServiceResult<InvoiceAdjustmentInfo>> GetAdjustmentInfoAsync(int actorId, int hoaDonId, CancellationToken ct = default)
        {
            if (actorId <= 0 || hoaDonId <= 0)
            {
                return ServiceResult<InvoiceAdjustmentInfo>.Fail("Dữ liệu không hợp lệ.");
            }

            try
            {
                var snap = await _autoStore.GetAdjustmentSnapshotAsync(hoaDonId, ct);
                if (snap == null)
                {
                    return ServiceResult<InvoiceAdjustmentInfo>.NotFound("Không tìm thấy hóa đơn.");
                }

                if (!await _employeeAccessService.CanPerformAsync(actorId, snap.ChiNhanhId, EmployeeActionCodes.InvoiceRead, ct))
                {
                    return ServiceResult<InvoiceAdjustmentInfo>.Forbidden("Bạn không có quyền xem hóa đơn tại chi nhánh này.");
                }

                var now = _time.GetUtcNow().UtcDateTime;
                var (lyDoKhoa, _) = await GetLockReasonAsync(actorId, snap, now, ct);
                return ServiceResult<InvoiceAdjustmentInfo>.Ok(new InvoiceAdjustmentInfo
                {
                    HoaDonId = snap.HoaDonId,
                    MaHoaDon = snap.MaHoaDon,
                    CoTheDieuChinh = lyDoKhoa == null,
                    LyDoKhoa = lyDoKhoa,
                    ChiSoDienCu = snap.ChiSoDienCu,
                    ChiSoDienMoi = snap.ChiSoDienMoi,
                    ChiSoNuocCu = snap.ChiSoNuocCu,
                    ChiSoNuocMoi = snap.ChiSoNuocMoi,
                    HanPhanHoiUtc = snap.HanPhanHoi
                });
            }
            finally
            {
                _autoStore.DiscardPendingChanges();
            }
        }

        public async Task<ServiceResult<InvoiceAdjustmentResult>> AdjustAsync(int actorId, AdjustInvoiceRequest request, CancellationToken ct = default)
        {
            if (actorId <= 0 || request == null || request.HoaDonId <= 0)
            {
                return ServiceResult<InvoiceAdjustmentResult>.Fail("Dữ liệu không hợp lệ.");
            }

            if (request.ChiSoDienMoi < 0 || request.ChiSoNuocMoi < 0)
            {
                return ServiceResult<InvoiceAdjustmentResult>.Fail("Chỉ số mới không được âm.");
            }

            if (((decimal.GetBits(request.ChiSoDienMoi)[3] >> 16) & 0x7F) > 3 || ((decimal.GetBits(request.ChiSoNuocMoi)[3] >> 16) & 0x7F) > 3)
            {
                return ServiceResult<InvoiceAdjustmentResult>.Fail("Chỉ số mới không được có quá 3 chữ số thập phân.");
            }

            var snap = await _autoStore.GetAdjustmentSnapshotAsync(request.HoaDonId, ct);
            _autoStore.DiscardPendingChanges();
            if (snap == null)
            {
                return ServiceResult<InvoiceAdjustmentResult>.NotFound("Không tìm thấy hóa đơn.");
            }

            if (!await _employeeAccessService.CanPerformAsync(actorId, snap.ChiNhanhId, EmployeeActionCodes.MeterReview, ct)
                || !await _employeeAccessService.CanPerformAsync(actorId, snap.ChiNhanhId, EmployeeActionCodes.InvoiceSubmit, ct))
            {
                return ServiceResult<InvoiceAdjustmentResult>.Forbidden(ThieuQuyen);
            }

            if (snap.DichVuDienNuocCuaPhongId == null)
            {
                return ServiceResult<InvoiceAdjustmentResult>.Fail(KhongCoKy);
            }

            var now = _time.GetUtcNow().UtcDateTime;
            var periodId = snap.DichVuDienNuocCuaPhongId.Value;
            var lyDoTag = AutoInvoiceHistoryTags.TaoLyDo(AutoInvoiceHistoryTags.DieuChinh, LyDoDieuChinh);

            await using var tx = await _unitOfWork.BeginTransactionAsync(ct);

            async Task<ServiceResult<InvoiceAdjustmentResult>> AbortAsync(ServiceResult<InvoiceAdjustmentResult> failure)
            {
                await TryRollbackAsync(tx, request.HoaDonId);
                _autoStore.DiscardPendingChanges();
                return failure;
            }

            HoaDon hoaDonGui;
            HoaDon hoaDon;
            DateTime han;
            MeterAdjustmentResult meter;
            bool thayThe;
            try
            {
                // Thứ tự khóa: kỳ, hợp đồng, hóa đơn (khớp CreateDraftsAsync).
                await _issuanceStore.LockMeterPeriodAsync(periodId, ct);
                await _issuanceStore.LockContractsAsync(new[] { snap.HopDongId }, ct);
                var locked = await _issuanceStore.GetInvoiceForUpdateAsync(request.HoaDonId, ct);
                if (locked == null)
                {
                    return await AbortAsync(ServiceResult<InvoiceAdjustmentResult>.NotFound("Không tìm thấy hóa đơn."));
                }

                hoaDon = locked;

                var current = new AutoInvoiceAdjustmentSnapshot
                {
                    HoaDonId = hoaDon.HoaDonId,
                    MaHoaDon = hoaDon.MaHoaDon,
                    HopDongId = hoaDon.HopDongId,
                    ChiNhanhId = snap.ChiNhanhId,
                    PhongTroId = snap.PhongTroId,
                    Thang = hoaDon.Thang,
                    Nam = hoaDon.Nam,
                    TrangThaiPhatHanh = hoaDon.TrangThaiPhatHanh,
                    TrangThaiHoaDon = hoaDon.TrangThaiHoaDon,
                    HanPhanHoi = hoaDon.HanPhanHoi,
                    DichVuDienNuocCuaPhongId = hoaDon.DichVuDienNuocCuaPhongId
                };
                var (lyDoKhoa, forbidden) = await GetLockReasonAsync(actorId, current, now, ct);
                if (lyDoKhoa != null)
                {
                    return await AbortAsync(forbidden
                        ? ServiceResult<InvoiceAdjustmentResult>.Forbidden(lyDoKhoa)
                        : ServiceResult<InvoiceAdjustmentResult>.Fail(lyDoKhoa));
                }

                var period = hoaDon.DichVuDienNuocCuaPhong;
                if (period == null)
                {
                    return await AbortAsync(ServiceResult<InvoiceAdjustmentResult>.Fail(KhongCoKy));
                }

                if (request.ChiSoDienMoi < period.ChiSoDienCu)
                {
                    return await AbortAsync(ServiceResult<InvoiceAdjustmentResult>.Fail("Chỉ số điện mới không được nhỏ hơn chỉ số điện cũ."));
                }

                if (request.ChiSoNuocMoi < period.ChiSoNuocCu)
                {
                    return await AbortAsync(ServiceResult<InvoiceAdjustmentResult>.Fail("Chỉ số nước mới không được nhỏ hơn chỉ số nước cũ."));
                }

                if (request.ChiSoDienMoi == period.ChiSoDienMoi && request.ChiSoNuocMoi == period.ChiSoNuocMoi)
                {
                    return await AbortAsync(ServiceResult<InvoiceAdjustmentResult>.Fail("Không có thay đổi"));
                }

                var meterRequest = new MeterAdjustmentRequest
                {
                    DichVuDienNuocCuaPhongId = periodId,
                    ChiSoDienMoi = request.ChiSoDienMoi,
                    ChiSoNuocMoi = request.ChiSoNuocMoi,
                    LyDo = LyDoDieuChinh
                };

                if (hoaDon.TrangThaiPhatHanh == TrangThaiPhatHanhHoaDon.ChoDuyet)
                {
                    // Nhánh ChoDuyet: mở lại bản tạm tính, tính lại tại chỗ rồi gửi lại với hạn mới.
                    thayThe = false;
                    hoaDon.MoLaiDeDieuChinh(actorId, now, lyDoTag);
                    await _unitOfWork.SaveChangesAsync(ct);

                    var adjusted = await _meterWorkflow.AdjustReadingsInCurrentTransactionAsync(meterRequest, actorId, ct);
                    if (!adjusted.Success || adjusted.Data == null)
                    {
                        return await AbortAsync(ServiceResult<InvoiceAdjustmentResult>.FailFrom(adjusted));
                    }

                    meter = adjusted.Data;
                    hoaDonGui = hoaDon;
                }
                else
                {
                    // Nhánh DaGui chưa thu đồng nào: hủy bản cũ, tạo bản thay thế bằng lõi tạo nháp.
                    thayThe = true;
                    foreach (var expired in await _paymentStore.GetExpiredWaitingRequestsAsync(hoaDon.HoaDonId, now, ct))
                    {
                        expired.DanhDauHetHan(now);
                    }

                    var active = await _paymentStore.GetActiveRequestAsync(hoaDon.HoaDonId, now, ct);
                    if (active?.TrangThai == TrangThaiYeuCauThanhToan.ChoThanhToan)
                    {
                        active.Huy(actorId, now, $"Hóa đơn bị hủy: {lyDoTag}");
                    }

                    hoaDon.HuyHoaDon(actorId, lyDoTag, now);
                    await _unitOfWork.SaveChangesAsync(ct);

                    var adjusted = await _meterWorkflow.AdjustReadingsInCurrentTransactionAsync(meterRequest, actorId, ct);
                    if (!adjusted.Success || adjusted.Data == null)
                    {
                        return await AbortAsync(ServiceResult<InvoiceAdjustmentResult>.FailFrom(adjusted));
                    }

                    meter = adjusted.Data;

                    var drafts = await _issuanceService.CreateDraftsInCurrentTransactionAsync(new CreateInvoiceDraftsRequest
                    {
                        ChiNhanhId = snap.ChiNhanhId,
                        Thang = hoaDon.Thang,
                        Nam = hoaDon.Nam,
                        PhongTroIds = new[] { snap.PhongTroId }
                    }, actorId, AutoInvoiceHistoryTags.TaoLyDo(AutoInvoiceHistoryTags.DieuChinh, "Tạo hóa đơn thay thế sau điều chỉnh"), ct);
                    if (!drafts.Success)
                    {
                        return await AbortAsync(ServiceResult<InvoiceAdjustmentResult>.FailFrom(drafts));
                    }

                    var items = drafts.Data?.Items ?? Array.Empty<InvoiceDraftItemResult>();
                    var created = items.Where(i => i.Status == InvoiceDraftItemStatus.Created && i.HoaDonId > 0).ToList();
                    if (created.Count != 1 || created[0].HopDongId != hoaDon.HopDongId)
                    {
                        var moTa = items.FirstOrDefault(i => i.HopDongId == hoaDon.HopDongId && i.Status != InvoiceDraftItemStatus.Created)?.Message
                            ?? (created.Count > 1 || created.Any(i => i.HopDongId != hoaDon.HopDongId)
                                ? "phòng có hợp đồng khác cần lập hóa đơn trong kỳ, xử lý theo luồng tay"
                                : "hợp đồng của hóa đơn cũ không còn được lập hóa đơn trong kỳ, xử lý theo luồng tay");
                        return await AbortAsync(ServiceResult<InvoiceAdjustmentResult>.Fail("Không tạo được hóa đơn thay thế: " + moTa));
                    }

                    var replacement = await _issuanceStore.GetInvoiceForUpdateAsync(created[0].HoaDonId!.Value, ct);
                    if (replacement == null)
                    {
                        return await AbortAsync(ServiceResult<InvoiceAdjustmentResult>.Fail("Không tạo được hóa đơn thay thế: không đọc lại được hóa đơn mới."));
                    }

                    hoaDonGui = replacement;
                }

                var activeDetails = hoaDonGui.ChiTietHoaDonDichVus.Where(x => !x.IsDeleted).ToList();
                if (activeDetails.Count == 0 || hoaDonGui.TongTien <= 0 || hoaDonGui.TongTien != activeDetails.Sum(x => x.TongTien))
                {
                    return await AbortAsync(ServiceResult<InvoiceAdjustmentResult>.Fail(TongTienLech));
                }

                var chiNhanh = await _hoaDonStore.GetChiNhanhByIdAsync(snap.ChiNhanhId, ct);
                if (chiNhanh == null || chiNhanh.SoGioPhanHoiHoaDon < 1 || chiNhanh.SoGioPhanHoiHoaDon > 168)
                {
                    return await AbortAsync(ServiceResult<InvoiceAdjustmentResult>.Fail("Chi nhánh không tồn tại hoặc số giờ phản hồi hóa đơn không hợp lệ."));
                }

                han = now.AddHours(chiNhanh.SoGioPhanHoiHoaDon);
                hoaDonGui.GuiTamTinh(actorId, now, han, AutoInvoiceHistoryTags.TaoLyDo(AutoInvoiceHistoryTags.DieuChinh, "Gửi lại hóa đơn tạm tính sau điều chỉnh"));
                await _unitOfWork.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
            }
            catch (UniqueConstraintViolationException ex)
            {
                await TryRollbackAsync(tx, request.HoaDonId);
                _autoStore.DiscardPendingChanges();
                _logger.LogInformation("Điều chỉnh hóa đơn {HoaDonId} vi phạm ràng buộc {Constraint}", request.HoaDonId, ex.ConstraintName);
                return ServiceResult<InvoiceAdjustmentResult>.Fail("Dữ liệu vừa thay đổi, vui lòng thử lại.");
            }
            catch (OperationCanceledException)
            {
                await TryRollbackAsync(tx, request.HoaDonId);
                _autoStore.DiscardPendingChanges();
                throw;
            }
            catch (Exception ex)
            {
                await TryRollbackAsync(tx, request.HoaDonId);
                _autoStore.DiscardPendingChanges();
                _logger.LogError(ex, "Lỗi khi điều chỉnh hóa đơn {HoaDonId}", request.HoaDonId);
                return ServiceResult<InvoiceAdjustmentResult>.Fail("Không thể điều chỉnh hóa đơn. Vui lòng thử lại.");
            }

            var result = new InvoiceAdjustmentResult
            {
                HoaDonIdCu = hoaDon.HoaDonId,
                HoaDonIdMoi = hoaDonGui.HoaDonId,
                MaHoaDonMoi = hoaDonGui.MaHoaDon,
                TongTienMoi = hoaDonGui.TongTien,
                HanPhanHoiUtc = han,
                DaTaoHoaDonThayThe = thayThe
            };
            var hopDongId = hoaDonGui.HopDongId;

            _autoStore.DiscardPendingChanges();
            await NotifyCustomerAsync(result, hopDongId, meter);

            return ServiceResult<InvoiceAdjustmentResult>.Ok(result, "Đã điều chỉnh và gửi lại hóa đơn tạm tính.");
        }

        // Trả (lý do khóa, có phải lỗi quyền). Xét theo thứ tự, trả lý do đầu tiên.
        private async Task<(string? Reason, bool Forbidden)> GetLockReasonAsync(int actorId, AutoInvoiceAdjustmentSnapshot s, DateTime now, CancellationToken ct)
        {
            if (s.HanPhanHoi == null)
            {
                return (KhongPhaiTuDong, false);
            }

            var daGui = s.TrangThaiPhatHanh == TrangThaiPhatHanhHoaDon.DaGui;
            if (s.TrangThaiPhatHanh != TrangThaiPhatHanhHoaDon.ChoDuyet && !daGui)
            {
                return (TrangThaiKhongHopLe, false);
            }

            var duQuyen = await _employeeAccessService.CanPerformAsync(actorId, s.ChiNhanhId, EmployeeActionCodes.MeterReview, ct)
                && await _employeeAccessService.CanPerformAsync(actorId, s.ChiNhanhId, EmployeeActionCodes.InvoiceSubmit, ct)
                && (!daGui || await _employeeAccessService.CanPerformAsync(actorId, s.ChiNhanhId, EmployeeActionCodes.InvoiceCancel, ct));
            if (!duQuyen)
            {
                return (ThieuQuyen, true);
            }

            if (s.DichVuDienNuocCuaPhongId == null)
            {
                return (KhongCoKy, false);
            }

            if (await _meterStore.HasSubsequentPeriodAsync(s.PhongTroId, s.Thang, s.Nam, ct))
            {
                return (KySauDaChot, false);
            }

            if (daGui)
            {
                if (s.TrangThaiHoaDon != TrangThaiHoaDon.ChuaThanhToan
                    || await _paymentStore.GetTongTienDaThanhToanHoaDonAsync(s.HoaDonId, ct) > 0)
                {
                    return (DaCoKhoanThu, false);
                }

                var active = await _paymentStore.GetActiveRequestAsync(s.HoaDonId, now, ct);
                if (active?.TrangThai == TrangThaiYeuCauThanhToan.DangDoiChieu)
                {
                    return (DangDoiChieu, false);
                }
            }

            return (null, false);
        }

        private async Task NotifyCustomerAsync(InvoiceAdjustmentResult result, int hopDongId, MeterAdjustmentResult meter)
        {
            InvoiceRecipientInfo? recipient = null;
            HoaDonChiTietRes? detail = null;
            try
            {
                recipient = await _publicationStore.GetRecipientAsync(hopDongId, CancellationToken.None);
                detail = await _hoaDonStore.GetHoaDonDetailByIdAsync(result.HoaDonIdMoi, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không đọc được dữ liệu để báo khách về hóa đơn đã điều chỉnh {HoaDonId}.", result.HoaDonIdMoi);
            }

            if (recipient == null || detail == null)
            {
                _logger.LogWarning("Không báo được khách về hóa đơn đã điều chỉnh {HoaDonId}: thiếu người nhận hoặc chi tiết hóa đơn.", result.HoaDonIdMoi);
                return;
            }

            var hanText = result.HanPhanHoiUtc.AddHours(7).ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);
            var noiDung = $"Hóa đơn {detail.MaHoaDon} tháng {detail.Thang}/{detail.Nam} đã được điều chỉnh: tổng tiền mới {detail.TongTien.ToString("#,##0", CultureInfo.InvariantCulture)} đ (tạm tính). "
                + $"Hệ thống tự chốt lúc {hanText}. Nếu có sai sót, vui lòng liên hệ {detail.SoDienThoaiChiNhanh}.";
            try
            {
                var sent = await _thongBaoService.GuiChoKhachThueAsync(
                    recipient.NguoiThueId,
                    $"Hóa đơn tháng {detail.Thang}/{detail.Nam} đã được điều chỉnh",
                    noiDung,
                    "HoaDon",
                    $"/KhachThue/HoaDon?hoaDonId={result.HoaDonIdMoi}");
                _autoStore.DiscardPendingChanges();
                if (!sent.Success)
                {
                    _logger.LogWarning("Không gửi được thông báo cổng về hóa đơn đã điều chỉnh {HoaDonId}: {Message}", result.HoaDonIdMoi, sent.Message);
                }
            }
            catch (Exception ex)
            {
                _autoStore.DiscardPendingChanges();
                _logger.LogWarning(ex, "Lỗi khi gửi thông báo cổng về hóa đơn đã điều chỉnh {HoaDonId}.", result.HoaDonIdMoi);
            }

            if (string.IsNullOrWhiteSpace(recipient.Email))
            {
                return;
            }

            try
            {
                var data = new ProvisionalInvoiceEmailData
                {
                    MaHoaDon = detail.MaHoaDon,
                    TenNguoiThue = detail.TenNguoiThue,
                    TenPhong = detail.TenPhong,
                    TenChiNhanh = detail.TenChiNhanh,
                    SoDienThoaiChiNhanh = detail.SoDienThoaiChiNhanh,
                    Thang = detail.Thang,
                    Nam = detail.Nam,
                    TongTien = detail.TongTien,
                    ChiTietHoaDons = detail.ChiTietHoaDons,
                    ChiSoDienCu = meter.ChiSoDienCu,
                    ChiSoDienMoi = meter.ChiSoDienMoi,
                    ChiSoNuocCu = meter.ChiSoNuocCu,
                    ChiSoNuocMoi = meter.ChiSoNuocMoi,
                    HanTuChotUtc = result.HanPhanHoiUtc,
                    LaDieuChinh = true
                };
                var email = await _emailService.SendProvisionalInvoiceEmailAsync(recipient.Email, data);
                if (!email.IsSuccess)
                {
                    _logger.LogWarning("Không gửi được email hóa đơn đã điều chỉnh {HoaDonId}: {Error}", result.HoaDonIdMoi, email.ErrorMessage);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Lỗi khi gửi email hóa đơn đã điều chỉnh {HoaDonId}.", result.HoaDonIdMoi);
            }
        }

        private async Task TryRollbackAsync(IApplicationTransaction tx, int hoaDonId)
        {
            try
            {
                // Rollback phải chạy cả khi request đã bị hủy, nên không dùng token của request.
                await tx.RollbackAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không thể rollback transaction điều chỉnh hóa đơn {HoaDonId}", hoaDonId);
            }
        }
    }
}
