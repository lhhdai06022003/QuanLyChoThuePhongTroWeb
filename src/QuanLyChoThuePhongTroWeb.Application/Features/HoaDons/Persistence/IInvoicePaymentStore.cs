using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Domain.Entities;

namespace QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Persistence
{
    public interface IInvoicePaymentStore
    {
        // ----- Ghi dưới khóa (entity được tracking) -----

        // SELECT ... FOR UPDATE dòng hoa_don, không lọc IsDeleted. Xóa change tracker trước khi khóa
        // để không trả về giá trị cũ đã tracking; vì vậy mọi luồng phải khóa trước khi đọc entity khác.
        Task<HoaDon?> LockHoaDonAsync(int hoaDonId, CancellationToken ct = default);
        Task<IReadOnlyList<YeuCauThanhToanHoaDon>> GetExpiredWaitingRequestsAsync(int hoaDonId, DateTime nowUtc, CancellationToken ct = default);
        Task<YeuCauThanhToanHoaDon?> GetActiveRequestAsync(int hoaDonId, DateTime nowUtc, CancellationToken ct = default);
        // Kèm minh chứng và lịch sử trạng thái.
        Task<YeuCauThanhToanHoaDon?> GetRequestAsync(int yeuCauId, CancellationToken ct = default);
        // Kèm lượt (và lịch sử của lượt).
        Task<MinhChungThanhToanHoaDon?> GetProofAsync(int minhChungId, CancellationToken ct = default);
        Task<LichSuThanhToan?> GetLedgerEntryAsync(int lichSuThanhToanId, CancellationToken ct = default);
        Task<decimal> GetTongTienDaThanhToanHoaDonAsync(int hoaDonId, CancellationToken ct = default);
        Task<bool> ExistsMaYeuCauAsync(string maYeuCau, CancellationToken ct = default);
        // So khớp không phân biệt hoa thường, chỉ với khoản ghi nhận chưa xóa mềm.
        Task<bool> ExistsMaGiaoDichAsync(string maGiaoDich, CancellationToken ct = default);
        Task<bool> ExistsPendingProofMaGiaoDichAsync(string maGiaoDich, int? exceptMinhChungId, CancellationToken ct = default);

        void AddRequest(YeuCauThanhToanHoaDon yeuCau);
        void AddLedgerEntry(LichSuThanhToan lichSu);
        void AddNotification(ThongBao thongBao);

        // ----- Người nhận thông báo -----

        Task<IReadOnlyList<int>> GetAdminUserIdsAsync(CancellationToken ct = default);
        // Admin và nhân viên đang được phân công chi nhánh (IsActive, chưa thu hồi).
        Task<IReadOnlyList<int>> GetReviewerUserIdsAsync(int chiNhanhId, CancellationToken ct = default);
        Task<int?> GetTenantUserIdAsync(int hopDongId, CancellationToken ct = default);

        // ----- Đọc trước khóa và read model (không tracking) -----

        Task<InvoicePaymentTarget?> GetTargetByHoaDonAsync(int hoaDonId, CancellationToken ct = default);
        Task<InvoicePaymentTarget?> GetTargetByYeuCauAsync(int yeuCauId, CancellationToken ct = default);
        Task<InvoicePaymentTarget?> GetTargetByMinhChungAsync(int minhChungId, CancellationToken ct = default);
        Task<InvoicePaymentTarget?> GetTargetByLedgerEntryAsync(int lichSuThanhToanId, CancellationToken ct = default);
        // Khớp chính xác, không phân biệt hoa thường; mọi trạng thái.
        Task<InvoicePaymentTarget?> GetTargetByMaYeuCauAsync(string maYeuCau, CancellationToken ct = default);

        Task<PaymentInvoiceSnapshot?> GetInvoiceSnapshotAsync(int hoaDonId, CancellationToken ct = default);
        // Mọi lượt chưa xóa của hóa đơn, mới nhất trước.
        Task<IReadOnlyList<PaymentRequestSnapshot>> GetRequestSnapshotsAsync(int hoaDonId, CancellationToken ct = default);
        Task<PaymentRequestSnapshot?> GetRequestSnapshotAsync(int yeuCauId, CancellationToken ct = default);
        Task<ReviewQueuePage> GetReviewQueueAsync(ReviewQueueQuery query, CancellationToken ct = default);
    }
}
