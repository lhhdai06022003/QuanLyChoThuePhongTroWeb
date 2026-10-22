using System.Threading;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.DTOs;

namespace QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Services
{
    public interface IInvoicePublicationService
    {
        Task<ServiceResult<InvoicePublishBatchResult>> PublishAsync(PublishInvoicesRequest request, int actorId, CancellationToken ct = default);
        Task<ServiceResult<InvoiceEmailResendResult>> ResendEmailAsync(int hoaDonId, int actorId, CancellationToken ct = default);
    }
}
