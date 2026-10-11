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
    // "Điều chỉnh và gửi lại" hóa đơn tự động theo yêu cầu khách thuê: sửa chỉ số điện nước và/hoặc các khoản phí của hóa đơn,
    // tính lại tổng tiền rồi gửi lại bản tạm tính với hạn mới.
    public class InvoiceAdjustmentService : IInvoiceAdjustmentService
    {
        // Spec: màn điều chỉnh không có ô lý do; lịch sử luôn ghi lý do này (kèm tóm tắt các dòng đã sửa khi gửi lại).
        private const string LyDoDieuChinh = "Điều chỉnh hóa đơn theo yêu cầu khách thuê";
        private const string KhongPhaiTuDong = "Chỉ hóa đơn tự động mới dùng được Điều chỉnh và gửi lại.";
        private const string TrangThaiKhongHopLe = "Hóa đơn không ở trạng thái điều chỉnh được.";
        private const string ThieuQuyen = "Bạn không có quyền điều chỉnh hóa đơn tại chi nhánh này.";
        private const string KhongCoKy = "Hóa đơn không gắn kỳ chỉ số điện nước.";
        private const string KySauDaChot = "Kỳ sau đã được chốt hoặc đã có hóa đơn phát hành, không thể điều chỉnh.";
        private const string DaCoKhoanThu = "Hóa đơn đã có khoản thu, không thể điều chỉnh.";
        private const string DangDoiChieu = "Khách đã nộp minh chứng, cần đối chiếu trước khi điều chỉnh.";
        private const string TongTienLech = "Tổng tiền hóa đơn không khớp với tổng chi tiết dịch vụ.";
        private const string KhongCoThayDoi = "Không có thay đổi";
        private const string CanItNhatMotDong = "Hóa đơn phải có ít nhất một khoản phí.";
        private const string TongTienPhaiDuong = "Tổng tiền hóa đơn phải lớn hơn 0.";

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
        private readonly IHoaDonCalculatorService _calculator;
        private readonly ILogger<InvoiceAdjustmentService> _logger;
        private readonly TimeProvider _time;
        private readonly IProvisionalInvoiceEmailDispatcher? _emailDispatcher;

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
            IHoaDonCalculatorService calculator,
            ILogger<InvoiceAdjustmentService> logger,
            TimeProvider? timeProvider = null,
            IProvisionalInvoiceEmailDispatcher? emailDispatcher = null)
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
            _calculator = calculator ?? throw new ArgumentNullException(nameof(calculator));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _time = timeProvider ?? TimeProvider.System;
            _emailDispatcher = emailDispatcher;
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
                    // Hóa đơn đã gửi có hạn cũ đã qua; chỉ bản tạm tính (chờ duyệt) mới có hạn tự chốt hiện tại.
                    HanPhanHoiUtc = snap.TrangThaiPhatHanh == TrangThaiPhatHanhHoaDon.ChoDuyet ? snap.HanPhanHoi : null,
                    TongTien = snap.TongTien,
                    ChiTiets = snap.ChiTiets.Select(ToLine).ToList()
                });
            }
            finally
            {
                _autoStore.DiscardPendingChanges();
            }
        }

        public async Task<ServiceResult<InvoiceMeterLinesPreview>> PreviewMeterLinesAsync(int actorId, int hoaDonId, decimal chiSoDienMoi, decimal chiSoNuocMoi, CancellationToken ct = default)
        {
            if (actorId <= 0 || hoaDonId <= 0)
            {
                return ServiceResult<InvoiceMeterLinesPreview>.Fail("Dữ liệu không hợp lệ.");
            }

            var loiChiSo = KiemTraChiSoMoi(chiSoDienMoi, chiSoNuocMoi);
            if (loiChiSo != null)
            {
                return ServiceResult<InvoiceMeterLinesPreview>.Fail(loiChiSo);
            }

            try
            {
                var snap = await _autoStore.GetAdjustmentSnapshotAsync(hoaDonId, ct);
                if (snap == null)
                {
                    return ServiceResult<InvoiceMeterLinesPreview>.NotFound("Không tìm thấy hóa đơn.");
                }

                if (!await _employeeAccessService.CanPerformAsync(actorId, snap.ChiNhanhId, EmployeeActionCodes.InvoiceRead, ct))
                {
                    return ServiceResult<InvoiceMeterLinesPreview>.Forbidden("Bạn không có quyền xem hóa đơn tại chi nhánh này.");
                }

                if (snap.DichVuDienNuocCuaPhongId == null)
                {
                    return ServiceResult<InvoiceMeterLinesPreview>.Fail(KhongCoKy);
                }

                if (chiSoDienMoi < snap.ChiSoDienCu)
                {
                    return ServiceResult<InvoiceMeterLinesPreview>.Fail("Chỉ số điện mới không được nhỏ hơn chỉ số điện cũ.");
                }

                if (chiSoNuocMoi < snap.ChiSoNuocCu)
                {
                    return ServiceResult<InvoiceMeterLinesPreview>.Fail("Chỉ số nước mới không được nhỏ hơn chỉ số nước cũ.");
                }

                // Cùng cách tính như lúc tạo hóa đơn và lúc tính lại hóa đơn nháp: tiền điện nước theo tỷ lệ số ngày ở của hợp đồng trong tháng.
                var tienPhong = _calculator.TinhTienPhong(snap.TienThuePhong, snap.HopDongBatDau, snap.HopDongKetThuc, snap.Thang, snap.Nam);
                var ngayTrongThang = DateTime.DaysInMonth(snap.Nam, snap.Thang);
                var dien = _calculator.TinhTienDienNuoc(chiSoDienMoi, snap.ChiSoDienCu, snap.DonGiaDien, "Tiền điện", tienPhong.SoNgayO, ngayTrongThang);
                var nuoc = _calculator.TinhTienDienNuoc(chiSoNuocMoi, snap.ChiSoNuocCu, snap.DonGiaNuoc, "Tiền nước", tienPhong.SoNgayO, ngayTrongThang);

                return ServiceResult<InvoiceMeterLinesPreview>.Ok(new InvoiceMeterLinesPreview
                {
                    Dien = dien.SoTien > 0 ? MeterPreviewLine(dien, snap.DonGiaDien, "dien", "kWh") : null,
                    Nuoc = nuoc.SoTien > 0 ? MeterPreviewLine(nuoc, snap.DonGiaNuoc, "nuoc", "m³") : null
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

            var loiChiSo = KiemTraChiSoMoi(request.ChiSoDienMoi, request.ChiSoNuocMoi);
            if (loiChiSo != null)
            {
                return ServiceResult<InvoiceAdjustmentResult>.Fail(loiChiSo);
            }

            // Danh sách dòng gửi lên (nếu có) là danh sách cuối cùng của hóa đơn, nên kiểm tra sớm để không mở giao dịch vô ích.
            List<InvoiceLineInput>? dongMoi = null;
            if (request.ChiTiets != null)
            {
                dongMoi = request.ChiTiets
                    .Select(l => new InvoiceLineInput(l.ChiTietHoaDonId, l.TenDichVu, l.DonGia, l.SoLuong))
                    .ToList();
                if (dongMoi.Count == 0)
                {
                    return ServiceResult<InvoiceAdjustmentResult>.Fail(CanItNhatMotDong);
                }

                var loiDong = InvoiceLineEditing.Validate(dongMoi);
                if (loiDong != null)
                {
                    return ServiceResult<InvoiceAdjustmentResult>.Fail(loiDong);
                }
            }

            var snap = await _autoStore.GetAdjustmentSnapshotAsync(request.HoaDonId, ct);
            _autoStore.DiscardPendingChanges();
            if (snap == null)
            {
                return ServiceResult<InvoiceAdjustmentResult>.NotFound("Không tìm thấy hóa đơn.");
            }

            if (!await CoDuQuyenDieuChinhAsync(actorId, snap.ChiNhanhId, ct))
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
            ChiSoKy chiSoKy;
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

                var chiSoDienMoi = request.ChiSoDienMoi ?? period.ChiSoDienMoi;
                var chiSoNuocMoi = request.ChiSoNuocMoi ?? period.ChiSoNuocMoi;
                if (chiSoDienMoi < period.ChiSoDienCu)
                {
                    return await AbortAsync(ServiceResult<InvoiceAdjustmentResult>.Fail("Chỉ số điện mới không được nhỏ hơn chỉ số điện cũ."));
                }

                if (chiSoNuocMoi < period.ChiSoNuocCu)
                {
                    return await AbortAsync(ServiceResult<InvoiceAdjustmentResult>.Fail("Chỉ số nước mới không được nhỏ hơn chỉ số nước cũ."));
                }

                // Chụp các dòng đang lưu trước mọi thay đổi: để so sánh với danh sách gửi lên và giữ thành tiền dòng không đổi.
                // Tóm tắt null nghĩa là danh sách gửi lên giống hệt các dòng đang lưu.
                var dongCu = InvoiceLineEditing.Baseline(hoaDon.ChiTietHoaDonDichVus);
                var tomTatDong = dongMoi != null ? InvoiceLineEditing.TomTat(dongMoi, dongCu) : null;
                var doiChiSo = chiSoDienMoi != period.ChiSoDienMoi || chiSoNuocMoi != period.ChiSoNuocMoi;
                var doiDong = tomTatDong != null;
                if (!doiChiSo && !doiDong)
                {
                    return await AbortAsync(ServiceResult<InvoiceAdjustmentResult>.Fail(KhongCoThayDoi));
                }

                // Đổi chỉ số kỳ (và tính lại hóa đơn nháp liên kết) bằng lõi chỉ số; gọi sau khi hóa đơn cũ đã về Nhap hoặc bị hủy.
                // Trả lỗi (đã rollback) nếu lõi chỉ số từ chối, null nếu không cần đổi hoặc đổi thành công.
                async Task<ServiceResult<InvoiceAdjustmentResult>?> DoiChiSoAsync()
                {
                    if (!doiChiSo)
                    {
                        return null;
                    }

                    var adjusted = await _meterWorkflow.AdjustReadingsInCurrentTransactionAsync(new MeterAdjustmentRequest
                    {
                        DichVuDienNuocCuaPhongId = periodId,
                        ChiSoDienMoi = chiSoDienMoi,
                        ChiSoNuocMoi = chiSoNuocMoi,
                        LyDo = LyDoDieuChinh
                    }, actorId, ct);
                    return adjusted.Success ? null : await AbortAsync(ServiceResult<InvoiceAdjustmentResult>.FailFrom(adjusted));
                }

                if (hoaDon.TrangThaiPhatHanh == TrangThaiPhatHanhHoaDon.ChoDuyet)
                {
                    // Nhánh ChoDuyet: mở lại bản tạm tính, sửa tại chỗ rồi gửi lại với hạn mới.
                    thayThe = false;
                    hoaDon.MoLaiDeDieuChinh(actorId, now, lyDoTag);
                    await _unitOfWork.SaveChangesAsync(ct);

                    if (await DoiChiSoAsync() is { } loiChiSoKy)
                    {
                        return loiChiSoKy;
                    }

                    if (doiDong)
                    {
                        ApDungDong(hoaDon, dongMoi!, dongCu, now);
                    }

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

                    if (await DoiChiSoAsync() is { } loiChiSoKy)
                    {
                        return loiChiSoKy;
                    }

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

                    if (doiDong)
                    {
                        // Dòng gửi lên tham chiếu các dòng của hóa đơn cũ; bản thay thế nhận đúng danh sách người dùng đã chốt.
                        ApDungDong(replacement, dongMoi!, dongCu, now);
                    }

                    hoaDonGui = replacement;
                }

                var activeDetails = hoaDonGui.ChiTietHoaDonDichVus.Where(x => !x.IsDeleted).ToList();
                if (doiDong && hoaDonGui.TongTien <= 0)
                {
                    return await AbortAsync(ServiceResult<InvoiceAdjustmentResult>.Fail(TongTienPhaiDuong));
                }

                if (activeDetails.Count == 0 || hoaDonGui.TongTien <= 0 || hoaDonGui.TongTien != activeDetails.Sum(x => x.TongTien))
                {
                    return await AbortAsync(ServiceResult<InvoiceAdjustmentResult>.Fail(TongTienLech));
                }

                var chiNhanh = await _hoaDonStore.GetChiNhanhByIdAsync(snap.ChiNhanhId, ct);
                if (chiNhanh == null || chiNhanh.SoGioPhanHoiHoaDon < 1 || chiNhanh.SoGioPhanHoiHoaDon > 168)
                {
                    return await AbortAsync(ServiceResult<InvoiceAdjustmentResult>.Fail("Chi nhánh không tồn tại hoặc số giờ phản hồi hóa đơn không hợp lệ."));
                }

                // Chỉ số hiển thị cho khách là chỉ số của kỳ sau khi điều chỉnh (cũ → mới), khớp các dòng điện/nước trên hóa đơn.
                // Dùng giá trị đã chốt ở trên thay vì đọc lại từ "period": lõi chỉ số tách kỳ ra khỏi bộ theo dõi rồi nạp lại bản mới,
                // nên biến "period" này có thể là bản cũ (chỉ số cũ của kỳ không đổi khi điều chỉnh).
                chiSoKy = new ChiSoKy(period.ChiSoDienCu, chiSoDienMoi, period.ChiSoNuocCu, chiSoNuocMoi);

                han = now.AddHours(chiNhanh.SoGioPhanHoiHoaDon);
                var lyDoGui = "Gửi lại hóa đơn tạm tính sau điều chỉnh" + (tomTatDong != null ? $" ({tomTatDong})" : string.Empty);
                hoaDonGui.GuiTamTinh(actorId, now, han, AutoInvoiceHistoryTags.TaoLyDo(AutoInvoiceHistoryTags.DieuChinh, lyDoGui));
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
                HoaDonIdMoi = hoaDonGui.HoaDonId,
                MaHoaDonMoi = hoaDonGui.MaHoaDon,
                TongTienMoi = hoaDonGui.TongTien,
                HanPhanHoiUtc = han,
                DaTaoHoaDonThayThe = thayThe
            };
            var hopDongId = hoaDonGui.HopDongId;

            _autoStore.DiscardPendingChanges();
            await NotifyCustomerAsync(result, hopDongId, chiSoKy);

            return ServiceResult<InvoiceAdjustmentResult>.Ok(result, "Đã điều chỉnh và gửi lại hóa đơn tạm tính.");
        }

        // Thay toàn bộ dòng khoản phí của hóa đơn (đang ở trạng thái nháp trong giao dịch) bằng danh sách người dùng đã chốt.
        private void ApDungDong(HoaDon hoaDon, IReadOnlyList<InvoiceLineInput> dongMoi, IReadOnlyDictionary<int, InvoiceLineBaseline> dongCu, DateTime now)
        {
            var dongs = InvoiceLineEditing.Build(hoaDon.HoaDonId, dongMoi, dongCu, dichVuIdTheoYeuCau: false);

            // Hóa đơn đang được theo dõi nên chỉ xóa dòng cũ và thêm dòng mới vào đúng tập hợp đó (không gọi Update, tránh đánh dấu sửa
            // cả hợp đồng, phòng, khách thuê đang nạp kèm). Tập hợp phải chỉ còn dòng mới để phép kiểm tổng bên dưới đúng.
            _hoaDonStore.RemoveChiTietHoaDons(hoaDon.ChiTietHoaDonDichVus.ToList());
            hoaDon.ChiTietHoaDonDichVus.Clear();
            foreach (var dong in dongs)
            {
                hoaDon.ChiTietHoaDonDichVus.Add(dong);
            }

            hoaDon.TongTien = dongs.Sum(x => x.TongTien);
            hoaDon.NgayCapNhat = now;
        }

        // Chỉ số của kỳ sau điều chỉnh (cũ → mới) để in trong email cho khách.
        private sealed record ChiSoKy(decimal DienCu, decimal DienMoi, decimal NuocCu, decimal NuocMoi);

        private async Task<bool> CoDuQuyenDieuChinhAsync(int actorId, int chiNhanhId, CancellationToken ct)
        {
            // Điều chỉnh gồm sửa chỉ số (duyệt chỉ số), sửa khoản phí (soạn hóa đơn) và gửi lại cho khách (gửi duyệt).
            return await _employeeAccessService.CanPerformAsync(actorId, chiNhanhId, EmployeeActionCodes.MeterReview, ct)
                && await _employeeAccessService.CanPerformAsync(actorId, chiNhanhId, EmployeeActionCodes.InvoiceDraft, ct)
                && await _employeeAccessService.CanPerformAsync(actorId, chiNhanhId, EmployeeActionCodes.InvoiceSubmit, ct);
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

            var duQuyen = await CoDuQuyenDieuChinhAsync(actorId, s.ChiNhanhId, ct)
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

        // Chỉ số mới nếu có phải không âm và tối đa 3 chữ số thập phân (cột decimal(18,3)).
        private static string? KiemTraChiSoMoi(params decimal?[] chiSos)
        {
            if (chiSos.Any(c => c < 0))
            {
                return "Chỉ số mới không được âm.";
            }

            if (chiSos.Any(c => c.HasValue && c.Value.Scale > 3))
            {
                return "Chỉ số mới không được có quá 3 chữ số thập phân.";
            }

            return null;
        }

        private static InvoiceAdjustmentLine ToLine(AutoInvoiceInvoiceLine l)
        {
            var kind = InvoiceMeterLine.Classify(l.LoaiDichVu, l.TenDichVu);
            return new InvoiceAdjustmentLine
            {
                ChiTietHoaDonId = l.ChiTietHoaDonId,
                TenDichVu = l.TenDichVu,
                DonGia = l.DonGia,
                SoLuong = l.SoLuong,
                TongTien = l.TongTien,
                Loai = kind switch
                {
                    InvoiceLineKind.Dien => "dien",
                    InvoiceLineKind.Nuoc => "nuoc",
                    _ => "khac"
                },
                DonVi = !string.IsNullOrWhiteSpace(l.DonVi)
                    ? l.DonVi!
                    : kind switch
                    {
                        InvoiceLineKind.Dien => "kWh",
                        InvoiceLineKind.Nuoc => "m³",
                        _ => string.Empty
                    }
            };
        }

        private static InvoiceAdjustmentLine MeterPreviewLine((decimal SoLuong, decimal SoTien, string DienGiai) data, decimal donGia, string loai, string donVi) => new()
        {
            ChiTietHoaDonId = 0,
            TenDichVu = data.DienGiai,
            DonGia = donGia,
            SoLuong = decimal.Round(data.SoLuong, 3, MidpointRounding.AwayFromZero),
            TongTien = data.SoTien,
            Loai = loai,
            DonVi = donVi
        };

        private async Task NotifyCustomerAsync(InvoiceAdjustmentResult result, int hopDongId, ChiSoKy chiSoKy)
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
                    ChiSoDienCu = chiSoKy.DienCu,
                    ChiSoDienMoi = chiSoKy.DienMoi,
                    ChiSoNuocCu = chiSoKy.NuocCu,
                    ChiSoNuocMoi = chiSoKy.NuocMoi,
                    HanTuChotUtc = result.HanPhanHoiUtc,
                    LaDieuChinh = true
                };

                // Gửi nền như email tạm tính khi chốt kỳ: màn điều chỉnh không phải chờ SMTP. Không có bộ gửi nền thì gửi ngay.
                if (_emailDispatcher != null)
                {
                    _emailDispatcher.Enqueue(new[] { new PendingProvisionalEmail(result.HoaDonIdMoi, recipient.Email, data) });
                }
                else
                {
                    var email = await _emailService.SendProvisionalInvoiceEmailAsync(recipient.Email, data);
                    if (!email.IsSuccess)
                    {
                        _logger.LogWarning("Không gửi được email hóa đơn đã điều chỉnh {HoaDonId}: {Error}", result.HoaDonIdMoi, email.ErrorMessage);
                    }
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
