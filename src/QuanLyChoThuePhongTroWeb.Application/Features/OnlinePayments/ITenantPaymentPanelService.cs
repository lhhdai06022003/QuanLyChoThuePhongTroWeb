using QuanLyChoThuePhongTroWeb.Application.Common.Models;

namespace QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;

// Cổng khách thuê (spec thanh toán trực tuyến §3.2). nguoiThueId lấy từ claim; hóa đơn không thuộc khách trả NotFound.
public interface ITenantPaymentPanelService
{
    Task<ServiceResult<TenantPaymentPanelDto>> GetPanelAsync(int hoaDonId, int nguoiThueId, CancellationToken ct = default);

    // Ảnh QR theo số còn lại của hóa đơn. Hóa đơn không nhận thanh toán được thì trả lỗi kèm lý do.
    Task<ServiceResult<InvoiceQrImage>> GetQrAsync(int hoaDonId, int nguoiThueId, CancellationToken ct = default);
}
