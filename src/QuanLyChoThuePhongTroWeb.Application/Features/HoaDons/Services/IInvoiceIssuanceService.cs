using System.Threading;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.DTOs;

namespace QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Services
{
    public interface IInvoiceIssuanceService
    {
        Task<ServiceResult<InvoiceDraftBatchResult>> CreateDraftsAsync(CreateInvoiceDraftsRequest request, int actorId, CancellationToken ct = default);
        // Lõi của CreateDraftsAsync chạy trong transaction do caller mở: không Begin/Commit/Rollback, có SaveChanges.
        // Kiểm quyền Invoice.Draft như CreateDraftsAsync. Exception ném ra cho caller rollback.
        Task<ServiceResult<InvoiceDraftBatchResult>> CreateDraftsInCurrentTransactionAsync(CreateInvoiceDraftsRequest request, int actorId, string? lyDoKhoiTao = null, CancellationToken ct = default);
        Task<ServiceResult<InvoiceTransitionResult>> SubmitAsync(int hoaDonId, int actorId, CancellationToken ct = default);
        // Gửi hóa đơn nháp cho khách xem tạm tính: Nhap -> ChoDuyet, đặt hạn phản hồi theo chi nhánh.
        Task<ServiceResult<InvoiceTransitionResult>> SubmitProvisionalAsync(int hoaDonId, int actorId, CancellationToken ct = default);
        Task<ServiceResult<InvoiceTransitionResult>> FinalizeAsync(int hoaDonId, int actorId, CancellationToken ct = default);
        Task<ServiceResult<InvoiceTransitionResult>> RejectAsync(int hoaDonId, string lyDo, int actorId, CancellationToken ct = default);
    }
}
