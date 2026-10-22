using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Notifications;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Persistence;
using QuanLyChoThuePhongTroWeb.Domain.Entities;

namespace QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Payments
{
    // Khung chung cho mọi thao tác ghi làm đổi lượt, minh chứng hoặc công nợ của một hóa đơn (spec §28):
    // mở transaction → khóa dòng hoa_don → ghi HetHan cho lượt quá hạn (§6.3) → chạy logic → lưu → commit
    // → đẩy thông báo SignalR sau commit (§24).
    //
    // Logic trả thất bại: rollback, trừ khi khung đã lưu HetHan và logic chưa tự lưu gì thì commit phần
    // hết hạn đó rồi mới trả lỗi (§6.4). Logic trả thành công thì lưu và commit. Vi phạm unique index
    // được đổi thành lỗi nghiệp vụ, không retry trong cùng transaction.
    public sealed class InvoicePaymentTransaction
    {
        private readonly IInvoicePaymentStore _store;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IThongBaoNotifier _notifier;
        private readonly ILogger<InvoicePaymentTransaction> _logger;
        private readonly TimeProvider _time;

        public InvoicePaymentTransaction(
            IInvoicePaymentStore store,
            IUnitOfWork unitOfWork,
            IThongBaoNotifier notifier,
            ILogger<InvoicePaymentTransaction> logger,
            TimeProvider? timeProvider = null)
        {
            _store = store;
            _unitOfWork = unitOfWork;
            _notifier = notifier;
            _logger = logger;
            _time = timeProvider ?? TimeProvider.System;
        }

        public DateTime UtcNow => _time.GetUtcNow().UtcDateTime;

        public async Task<ServiceResult<T>> RunAsync<T>(
            int hoaDonId,
            int? actorId,
            Func<InvoicePaymentScope, Task<ServiceResult<T>>> work,
            CancellationToken ct = default)
        {
            await using var tx = await _unitOfWork.BeginTransactionAsync(ct);
            InvoicePaymentScope scope;
            ServiceResult<T> result;

            try
            {
                var hoaDon = await _store.LockHoaDonAsync(hoaDonId, ct);
                if (hoaDon == null)
                {
                    await TryRollbackAsync(tx, hoaDonId);
                    return ServiceResult<T>.NotFound("Không tìm thấy hóa đơn.");
                }

                var now = UtcNow;
                var expired = await _store.GetExpiredWaitingRequestsAsync(hoaDonId, now, ct);
                foreach (var yeuCau in expired)
                {
                    yeuCau.DanhDauHetHan(now);
                }

                if (expired.Count > 0)
                {
                    await _unitOfWork.SaveChangesAsync(ct);
                }

                var daThu = await _store.GetTongTienDaThanhToanHoaDonAsync(hoaDonId, ct);
                scope = new InvoicePaymentScope(_store, _unitOfWork, hoaDon, actorId, now, daThu,
                    expired.Select(y => y.YeuCauThanhToanHoaDonId).ToHashSet(), ct);

                result = await work(scope);

                if (!result.Success)
                {
                    if (expired.Count > 0 && !scope.HasSavedChanges)
                    {
                        // Chỉ commit phần HetHan đã lưu; các thay đổi logic chưa lưu bị bỏ.
                        await tx.CommitAsync(ct);
                    }
                    else
                    {
                        await TryRollbackAsync(tx, hoaDonId);
                    }

                    return result;
                }

                await _unitOfWork.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
            }
            catch (UniqueConstraintViolationException ex)
            {
                await TryRollbackAsync(tx, hoaDonId);
                _logger.LogInformation("Thao tác thanh toán hóa đơn {HoaDonId} vi phạm ràng buộc {Constraint}", hoaDonId, ex.ConstraintName);
                return ServiceResult<T>.Fail(MessageForConstraint(ex.ConstraintName));
            }
            catch (OperationCanceledException)
            {
                await TryRollbackAsync(tx, hoaDonId);
                throw;
            }
            catch (Exception ex)
            {
                await TryRollbackAsync(tx, hoaDonId);
                _logger.LogError(ex, "Lỗi khi xử lý thanh toán hóa đơn {HoaDonId} bởi {ActorId}", hoaDonId, actorId);
                return ServiceResult<T>.Fail("Đã có lỗi hệ thống, vui lòng thử lại.");
            }

            await PushNotificationsAsync(scope.Notifications, hoaDonId);
            return result;
        }

        private static string MessageForConstraint(string constraintName)
        {
            if (constraintName.Contains("MaGiaoDich", StringComparison.OrdinalIgnoreCase))
            {
                return "Mã giao dịch đã được ghi nhận.";
            }

            if (constraintName.Contains("MinhChungThanhToanHoaDonId", StringComparison.OrdinalIgnoreCase))
            {
                return "Minh chứng này đã được ghi nhận trước đó.";
            }

            return "Dữ liệu vừa thay đổi, vui lòng thử lại.";
        }

        private async Task PushNotificationsAsync(IReadOnlyList<ThongBao> notifications, int hoaDonId)
        {
            foreach (var thongBao in notifications)
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
                    _logger.LogWarning(ex, "Lỗi khi gửi thông báo SignalR thanh toán hóa đơn {HoaDonId} cho người dùng {NguoiDungId}", hoaDonId, thongBao.NguoiDungId);
                }
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
                _logger.LogWarning(ex, "Không thể rollback transaction thanh toán hóa đơn {HoaDonId}", hoaDonId);
            }
        }
    }

    // Trạng thái của một lần chạy InvoicePaymentTransaction, truyền cho logic nghiệp vụ.
    public sealed class InvoicePaymentScope
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly CancellationToken _ct;
        private readonly List<ThongBao> _notifications = new();

        internal InvoicePaymentScope(
            IInvoicePaymentStore store,
            IUnitOfWork unitOfWork,
            HoaDon hoaDon,
            int? actorId,
            DateTime nowUtc,
            decimal daThu,
            IReadOnlySet<int> expiredRequestIds,
            CancellationToken ct)
        {
            Store = store;
            _unitOfWork = unitOfWork;
            HoaDon = hoaDon;
            ActorId = actorId;
            NowUtc = nowUtc;
            DaThu = daThu;
            ExpiredRequestIds = expiredRequestIds;
            _ct = ct;
        }

        public IInvoicePaymentStore Store { get; }
        public HoaDon HoaDon { get; }
        public int? ActorId { get; }
        public DateTime NowUtc { get; }
        public CancellationToken CancellationToken => _ct;

        // Tổng ledger chưa xóa mềm, đọc lại trong transaction sau khi khóa.
        public decimal DaThu { get; private set; }
        public decimal ConLai => HoaDon.TongTien - DaThu;

        // Lượt vừa được khung ghi HetHan trong lần chạy này.
        public IReadOnlySet<int> ExpiredRequestIds { get; }

        internal bool HasSavedChanges { get; private set; }
        internal IReadOnlyList<ThongBao> Notifications => _notifications;

        // Lưu thay đổi ledger, đọc lại tổng từ DB rồi tính lại trạng thái hóa đơn (spec §8).
        // Vi phạm unique index ném lên khung và được đổi thành lỗi nghiệp vụ.
        public async Task ApplyLedgerChangeAsync(string? lyDo, bool luonGhiLichSu = false)
        {
            await SaveChangesAsync();
            DaThu = await Store.GetTongTienDaThanhToanHoaDonAsync(HoaDon.HoaDonId, _ct);
            HoaDon.CapNhatTrangThaiThanhToan(DaThu, ActorId, lyDo, NowUtc, luonGhiLichSu);
        }

        // Lưu sớm khi logic cần id vừa sinh; sau đó logic không được trả thất bại (khung sẽ rollback).
        public async Task SaveChangesAsync()
        {
            await _unitOfWork.SaveChangesAsync(_ct);
            HasSavedChanges = true;
        }

        public void Notify(int nguoiDungId, string tieuDe, string noiDung, string linkDieuHuong)
        {
            if (_notifications.Any(n => n.NguoiDungId == nguoiDungId && n.TieuDe == tieuDe && n.NoiDung == noiDung))
            {
                return;
            }

            var thongBao = new ThongBao
            {
                NguoiDungId = nguoiDungId,
                TieuDe = tieuDe,
                NoiDung = noiDung,
                LinhVuc = "HoaDon",
                LinkDieuHuong = linkDieuHuong,
                CreatedAt = NowUtc
            };
            Store.AddNotification(thongBao);
            _notifications.Add(thongBao);
        }

        public async Task NotifyTenantAsync(string tieuDe, string noiDung)
        {
            var userId = await Store.GetTenantUserIdAsync(HoaDon.HopDongId, _ct);
            if (userId.HasValue)
            {
                Notify(userId.Value, tieuDe, noiDung, $"/KhachThue/HoaDon?hoaDonId={HoaDon.HoaDonId}");
            }
        }

        public async Task NotifyAdminsAsync(string tieuDe, string noiDung, string linkDieuHuong)
        {
            foreach (var userId in await Store.GetAdminUserIdsAsync(_ct))
            {
                Notify(userId, tieuDe, noiDung, linkDieuHuong);
            }
        }

        public async Task NotifyReviewersAsync(int chiNhanhId, string tieuDe, string noiDung, string linkDieuHuong)
        {
            foreach (var userId in await Store.GetReviewerUserIdsAsync(chiNhanhId, _ct))
            {
                Notify(userId, tieuDe, noiDung, linkDieuHuong);
            }
        }
    }
}
