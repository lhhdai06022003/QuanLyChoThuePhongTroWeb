using System.Threading;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.DTOs;

namespace QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Services
{
    public interface IInvoiceAdjustmentService
    {
        Task<ServiceResult<InvoiceAdjustmentInfo>> GetAdjustmentInfoAsync(int actorId, int hoaDonId, CancellationToken ct = default);
        Task<ServiceResult<InvoiceAdjustmentResult>> AdjustAsync(int actorId, AdjustInvoiceRequest request, CancellationToken ct = default);
    }
}
