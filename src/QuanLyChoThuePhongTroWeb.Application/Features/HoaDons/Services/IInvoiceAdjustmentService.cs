using System.Threading;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.DTOs;

namespace QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Services
{
    public interface IInvoiceAdjustmentService
    {
        Task<ServiceResult<InvoiceAdjustmentInfo>> GetAdjustmentInfoAsync(int actorId, int hoaDonId, CancellationToken ct = default);
        // Tính lại dòng điện/nước theo chỉ số mới để màn điều chỉnh xem trước; không ghi dữ liệu.
        Task<ServiceResult<InvoiceMeterLinesPreview>> PreviewMeterLinesAsync(int actorId, int hoaDonId, decimal chiSoDienMoi, decimal chiSoNuocMoi, CancellationToken ct = default);
        Task<ServiceResult<InvoiceAdjustmentResult>> AdjustAsync(int actorId, AdjustInvoiceRequest request, CancellationToken ct = default);
    }
}
