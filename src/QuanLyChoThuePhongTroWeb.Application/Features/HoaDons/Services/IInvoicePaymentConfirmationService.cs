using System.Threading;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Services;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.DTOs;

namespace QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Services
{
    // Phía quản lý (spec §23.2): nhân viên được phân công chi nhánh của hóa đơn hoặc Admin.
    public interface IInvoicePaymentConfirmationService
    {
        Task<ServiceResult<DataTableResponse<ReviewQueueRowDto>>> GetReviewQueueAsync(int actorId, ReviewQueueFilter filter, DataTableRequest request, CancellationToken ct = default);
        Task<ServiceResult<PaymentRequestDetailDto>> GetProofDetailAsync(int minhChungId, int actorId, CancellationToken ct = default);
        Task<ServiceResult<MeterImageReadResult>> GetProofImageAsync(int minhChungId, int actorId, CancellationToken ct = default);
        Task<ServiceResult<ConfirmPaymentResult>> ConfirmAsync(ConfirmPaymentRequest request, int actorId, CancellationToken ct = default);
        Task<ServiceResult<bool>> RejectAsync(int minhChungId, int actorId, string lyDo, CancellationToken ct = default);
        Task<ServiceResult<InvoicePaymentSummaryDto>> ConfigurePartialPaymentAsync(ConfigurePartialPaymentRequest request, int actorId, CancellationToken ct = default);
        Task<ServiceResult<PaymentRequestDetailDto>> SearchPaymentRequestAsync(string maYeuCau, int actorId, CancellationToken ct = default);
        Task<ServiceResult<InvoicePaymentSummaryDto>> GetInvoicePaymentSummaryAsync(int hoaDonId, int actorId, CancellationToken ct = default);
    }
}
