using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Notifications;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Security;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Services;
using QuanLyChoThuePhongTroWeb.Application.Common.Configurations;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Common.Security;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Persistence;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Services
{
    public class InvoicePublicationService : IInvoicePublicationService
    {
        private readonly IInvoicePublicationStore _store;
        private readonly IHoaDonStore _hoaDonStore;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IEmployeeAccessService _employeeAccessService;
        private readonly IEmailService _emailService;
        private readonly IInvoiceDocumentExporter _documentExporter;
        private readonly IVietQRService _vietQRService;
        private readonly VietQrSettings _vietQrSettings;
        private readonly IThongBaoNotifier _notifier;
        private readonly InvoiceIssuanceOptions _options;
        private readonly ILogger<InvoicePublicationService> _logger;

        private const int MaxBatchSize = 50;

        public InvoicePublicationService(
            IInvoicePublicationStore store,
            IHoaDonStore hoaDonStore,
            IUnitOfWork unitOfWork,
            IEmployeeAccessService employeeAccessService,
            IEmailService emailService,
            IInvoiceDocumentExporter documentExporter,
            IVietQRService vietQRService,
            VietQrSettings vietQrSettings,
            IThongBaoNotifier notifier,
            InvoiceIssuanceOptions options,
            ILogger<InvoicePublicationService> logger)
        {
            _store = store;
            _hoaDonStore = hoaDonStore;
            _unitOfWork = unitOfWork;
            _employeeAccessService = employeeAccessService;
            _emailService = emailService;
            _documentExporter = documentExporter;
            _vietQRService = vietQRService;
            _vietQrSettings = vietQrSettings;
            _notifier = notifier;
            _options = options;
            _logger = logger;
        }

        public async Task<ServiceResult<InvoicePublishBatchResult>> PublishAsync(PublishInvoicesRequest request, int actorId, CancellationToken ct = default)
        {
            if (actorId <= 0)
            {
                return ServiceResult<InvoicePublishBatchResult>.Fail("Người thực hiện không hợp lệ.");
            }

            if (request?.HoaDonIds == null || request.HoaDonIds.Count == 0)
            {
                return ServiceResult<InvoicePublishBatchResult>.Fail("Vui lòng chọn ít nhất một hóa đơn để công bố.");
            }

            if (request.HoaDonIds.Any(id => id <= 0))
            {
                return ServiceResult<InvoicePublishBatchResult>.Fail("Mã hóa đơn không hợp lệ.");
            }

            var ids = request.HoaDonIds.Distinct().OrderBy(x => x).ToList();
            if (ids.Count > MaxBatchSize)
            {
                return ServiceResult<InvoicePublishBatchResult>.Fail("Mỗi lần chỉ được công bố tối đa 50 hóa đơn.");
            }

            var items = new List<InvoicePublishItemResult>();
            var publishedCount = 0;

            foreach (var id in ids)
            {
                if (ct.IsCancellationRequested)
                {
                    break;
                }

                InvoicePublicationSnapshot? snapshot = null;
                PublishTransactionResult? txResult = null;

                try
                {
                    snapshot = await _store.GetSnapshotAsync(id, ct);
                    if (snapshot == null)
                    {
                        items.Add(BuildSimpleResult(id, null, InvoicePublishOutcome.NotFound));
                        continue;
                    }

                    if (snapshot.IsDeleted || snapshot.TrangThaiPhatHanh == TrangThaiPhatHanhHoaDon.DaHuy)
                    {
                        items.Add(BuildSimpleResult(id, snapshot.MaHoaDon, InvoicePublishOutcome.Cancelled));
                        continue;
                    }

                    var canPublish = await _employeeAccessService.CanPerformAsync(actorId, snapshot.ChiNhanhId, EmployeeActionCodes.InvoiceSend, ct);
                    if (!canPublish)
                    {
                        items.Add(BuildSimpleResult(id, snapshot.MaHoaDon, InvoicePublishOutcome.Forbidden));
                        continue;
                    }

                    txResult = await PublishOneInTransactionAsync(id, snapshot.MaHoaDon, actorId, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi khi công bố hóa đơn {HoaDonId} bởi actor {ActorId}", id, actorId);
                    items.Add(BuildSimpleResult(id, snapshot?.MaHoaDon, InvoicePublishOutcome.Error));
                    continue;
                }

                if (txResult.Outcome != InvoicePublishOutcome.Published)
                {
                    items.Add(BuildSimpleResult(id, txResult.MaHoaDon, txResult.Outcome));
                    continue;
                }

                var hoaDon = txResult.HoaDon!;
                var thongBao = txResult.ThongBao;
                var recipient = txResult.Recipient;

                if (thongBao != null)
                {
                    try
                    {
                        await _notifier.SendToUserAsync(thongBao.NguoiDungId!.Value, new
                        {
                            id = thongBao.Id,
                            tieuDe = thongBao.TieuDe,
                            noiDung = thongBao.NoiDung,
                            linhVuc = thongBao.LinhVuc,
                            linkDieuHuong = thongBao.LinkDieuHuong,
                            createdAt = thongBao.CreatedAt.AddHours(7).ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture)
                        }, CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Lỗi khi gửi thông báo SignalR cho hóa đơn {HoaDonId}", hoaDon.HoaDonId);
                    }
                }

                var (emailOutcome, emailMessage) = await SendInvoiceEmailCoreAsync(hoaDon.HoaDonId, recipient?.Email, actorId);

                items.Add(new InvoicePublishItemResult
                {
                    HoaDonId = hoaDon.HoaDonId,
                    MaHoaDon = hoaDon.MaHoaDon,
                    Publish = InvoicePublishOutcome.Published,
                    PublishMessage = "Đã công bố",
                    PortalNotification = thongBao != null ? InvoicePortalNotificationOutcome.Created : InvoicePortalNotificationOutcome.NoPortalAccount,
                    Email = emailOutcome,
                    EmailMessage = emailMessage,
                    RecipientEmail = recipient?.Email,
                    HanThanhToanUtc = hoaDon.HanThanhToan
                });
                publishedCount++;
            }

            return ServiceResult<InvoicePublishBatchResult>.Ok(new InvoicePublishBatchResult
            {
                Items = items,
                PublishedCount = publishedCount
            });
        }

        public async Task<ServiceResult<InvoiceEmailResendResult>> ResendEmailAsync(int hoaDonId, int actorId, CancellationToken ct = default)
        {
            if (actorId <= 0)
            {
                return ServiceResult<InvoiceEmailResendResult>.Fail("Người thực hiện không hợp lệ.");
            }

            var snapshot = await _store.GetSnapshotAsync(hoaDonId, ct);
            if (snapshot == null)
            {
                return ServiceResult<InvoiceEmailResendResult>.Fail("Không tìm thấy hóa đơn.");
            }

            var canResend = await _employeeAccessService.CanPerformAsync(actorId, snapshot.ChiNhanhId, EmployeeActionCodes.InvoiceResendEmail, ct);
            if (!canResend)
            {
                return ServiceResult<InvoiceEmailResendResult>.Fail("Bạn không có quyền gửi lại email hóa đơn tại chi nhánh này.");
            }

            if (snapshot.IsDeleted || snapshot.TrangThaiPhatHanh == TrangThaiPhatHanhHoaDon.DaHuy)
            {
                return ServiceResult<InvoiceEmailResendResult>.Fail("Hóa đơn đã bị hủy, không thể gửi lại email.");
            }

            if (snapshot.TrangThaiPhatHanh != TrangThaiPhatHanhHoaDon.DaGui)
            {
                return ServiceResult<InvoiceEmailResendResult>.Fail("Chỉ hóa đơn đã công bố mới được gửi lại email.");
            }

            var recipient = await _store.GetRecipientAsync(snapshot.HopDongId, ct);
            var (emailOutcome, emailMessage) = await SendInvoiceEmailCoreAsync(hoaDonId, recipient?.Email, actorId);

            return ServiceResult<InvoiceEmailResendResult>.Ok(new InvoiceEmailResendResult
            {
                HoaDonId = hoaDonId,
                MaHoaDon = snapshot.MaHoaDon,
                Email = emailOutcome,
                EmailMessage = emailMessage,
                RecipientEmail = recipient?.Email
            });
        }

        private sealed class PublishTransactionResult
        {
            public InvoicePublishOutcome Outcome { get; init; }
            public string? MaHoaDon { get; init; }
            public HoaDon? HoaDon { get; init; }
            public ThongBao? ThongBao { get; init; }
            public InvoiceRecipientInfo? Recipient { get; init; }
        }

        private async Task<PublishTransactionResult> PublishOneInTransactionAsync(int hoaDonId, string snapshotMaHoaDon, int actorId, CancellationToken ct)
        {
            await using var tx = await _unitOfWork.BeginTransactionAsync(ct);
            var committed = false;

            try
            {
                var hoaDon = await _store.LockInvoiceAsync(hoaDonId, ct);
                if (hoaDon == null)
                {
                    await TryRollbackAsync(tx, hoaDonId, actorId);
                    return new PublishTransactionResult { Outcome = InvoicePublishOutcome.NotFound, MaHoaDon = snapshotMaHoaDon };
                }

                if (hoaDon.TrangThaiPhatHanh == TrangThaiPhatHanhHoaDon.DaGui)
                {
                    await TryRollbackAsync(tx, hoaDonId, actorId);
                    return new PublishTransactionResult { Outcome = InvoicePublishOutcome.AlreadyPublished, MaHoaDon = hoaDon.MaHoaDon };
                }

                if (hoaDon.TrangThaiPhatHanh == TrangThaiPhatHanhHoaDon.DaHuy || hoaDon.IsDeleted)
                {
                    await TryRollbackAsync(tx, hoaDonId, actorId);
                    return new PublishTransactionResult { Outcome = InvoicePublishOutcome.Cancelled, MaHoaDon = hoaDon.MaHoaDon };
                }

                if (hoaDon.TrangThaiPhatHanh != TrangThaiPhatHanhHoaDon.DaChot)
                {
                    await TryRollbackAsync(tx, hoaDonId, actorId);
                    return new PublishTransactionResult { Outcome = InvoicePublishOutcome.InvalidState, MaHoaDon = hoaDon.MaHoaDon };
                }

                var recipient = await _store.GetRecipientAsync(hoaDon.HopDongId, ct);
                var now = DateTime.UtcNow;
                var hanMacDinh = InvoiceDueDateCalculator.Compute(now, _options.SoNgayHanThanhToan);
                hoaDon.GuiHoaDon(actorId, now, hanMacDinh, "Công bố hóa đơn lên cổng khách thuê");

                ThongBao? thongBao = null;
                if (recipient?.NguoiDungId is int nguoiDungId)
                {
                    var noiDung = $"Hóa đơn {hoaDon.MaHoaDon} tháng {hoaDon.Thang}/{hoaDon.Nam}: tổng tiền {string.Format("{0:#,##0}", hoaDon.TongTien)} đ, hạn thanh toán hết ngày {hoaDon.HanThanhToan!.Value.AddHours(7).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)}.";
                    thongBao = new ThongBao
                    {
                        NguoiDungId = nguoiDungId,
                        TieuDe = $"Hóa đơn tháng {hoaDon.Thang}/{hoaDon.Nam}",
                        NoiDung = noiDung,
                        LinhVuc = "HoaDon",
                        LinkDieuHuong = $"/KhachThue/HoaDon?hoaDonId={hoaDon.HoaDonId}",
                        CreatedAt = now
                    };
                    _store.AddNotification(thongBao);
                }

                await _unitOfWork.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                committed = true;

                return new PublishTransactionResult
                {
                    Outcome = InvoicePublishOutcome.Published,
                    MaHoaDon = hoaDon.MaHoaDon,
                    HoaDon = hoaDon,
                    ThongBao = thongBao,
                    Recipient = recipient
                };
            }
            catch (Exception ex)
            {
                if (!committed)
                {
                    await TryRollbackAsync(tx, hoaDonId, actorId);
                }

                _logger.LogError(ex, "Lỗi khi công bố hóa đơn {HoaDonId} bởi actor {ActorId}", hoaDonId, actorId);
                return new PublishTransactionResult { Outcome = InvoicePublishOutcome.Error, MaHoaDon = snapshotMaHoaDon };
            }
        }

        private async Task TryRollbackAsync(IApplicationTransaction tx, int hoaDonId, int actorId)
        {
            try
            {
                // Rollback phải chạy cả khi request đã bị hủy, nên không dùng token của request.
                await tx.RollbackAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không thể rollback transaction công bố hóa đơn {HoaDonId} bởi actor {ActorId}", hoaDonId, actorId);
            }
        }

        private async Task<(InvoiceEmailOutcome Outcome, string Message)> SendInvoiceEmailCoreAsync(int hoaDonId, string? recipientEmail, int actorId)
        {
            if (string.IsNullOrWhiteSpace(recipientEmail))
            {
                return (InvoiceEmailOutcome.NoEmail, "Khách thuê chưa có địa chỉ email.");
            }

            try
            {
                var detail = await _hoaDonStore.GetHoaDonDetailByIdAsync(hoaDonId, CancellationToken.None);
                if (detail == null)
                {
                    return (InvoiceEmailOutcome.Failed, "Gửi email thất bại. Có thể dùng Gửi lại email sau.");
                }

                var pdf = InvoicePdfComposer.Compose(detail, _vietQRService, _vietQrSettings, _documentExporter);
                if (pdf == null || pdf.Length == 0)
                {
                    return (InvoiceEmailOutcome.Failed, "Gửi email thất bại. Có thể dùng Gửi lại email sau.");
                }

                var (ok, err) = await _emailService.SendInvoiceEmailAsync(recipientEmail, detail, pdf);
                if (!ok)
                {
                    _logger.LogWarning(
                        "Gửi email hóa đơn {HoaDonId} thất bại bởi actor {ActorId}: {Error}",
                        hoaDonId,
                        actorId,
                        err);
                    return (InvoiceEmailOutcome.Failed, "Gửi email thất bại. Có thể dùng Gửi lại email sau.");
                }

                return (InvoiceEmailOutcome.Sent, "Đã gửi email hóa đơn.");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Lỗi khi gửi email hóa đơn {HoaDonId} bởi actor {ActorId}", hoaDonId, actorId);
                return (InvoiceEmailOutcome.Failed, "Gửi email thất bại. Có thể dùng Gửi lại email sau.");
            }
        }

        private static InvoicePublishItemResult BuildSimpleResult(int hoaDonId, string? maHoaDon, InvoicePublishOutcome outcome)
        {
            return new InvoicePublishItemResult
            {
                HoaDonId = hoaDonId,
                MaHoaDon = maHoaDon,
                Publish = outcome,
                PublishMessage = PublishMessageFor(outcome),
                PortalNotification = InvoicePortalNotificationOutcome.NotApplicable,
                Email = InvoiceEmailOutcome.NotAttempted,
                EmailMessage = null,
                RecipientEmail = null,
                HanThanhToanUtc = null
            };
        }

        private static string PublishMessageFor(InvoicePublishOutcome outcome) => outcome switch
        {
            InvoicePublishOutcome.AlreadyPublished => "Đã công bố trước đó",
            InvoicePublishOutcome.NotFound => "Không tìm thấy hóa đơn.",
            InvoicePublishOutcome.Forbidden => "Bạn không có quyền công bố hóa đơn tại chi nhánh này.",
            InvoicePublishOutcome.Cancelled => "Hóa đơn đã bị hủy, không thể công bố.",
            InvoicePublishOutcome.InvalidState => "Chỉ hóa đơn đã chốt mới được công bố.",
            InvoicePublishOutcome.Error => "Không thể công bố hóa đơn. Vui lòng thử lại.",
            _ => string.Empty
        };
    }
}
