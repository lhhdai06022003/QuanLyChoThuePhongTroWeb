using System.Threading;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.DTOs;

namespace QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Services
{
    // Các thao tác trực tiếp lên công nợ hóa đơn: thu thủ công (spec §18), hủy ghi nhận (§20), hủy hóa đơn (§31.2).
    public interface IInvoiceLedgerService
    {
        Task<ServiceResult<ManualCollectionResult>> CollectManuallyAsync(ManualCollectionRequest request, int actorId, CancellationToken ct = default);
        Task<ServiceResult<VoidPaymentResult>> VoidPaymentAsync(int lichSuThanhToanId, int actorId, string lyDo, CancellationToken ct = default);
        Task<ServiceResult<bool>> CancelInvoiceAsync(int hoaDonId, int actorId, string lyDo, CancellationToken ct = default);
    }
}
