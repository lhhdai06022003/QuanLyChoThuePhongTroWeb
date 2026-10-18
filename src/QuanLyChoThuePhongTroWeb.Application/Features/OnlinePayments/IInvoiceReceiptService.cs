using QuanLyChoThuePhongTroWeb.Application.Common.Models;

namespace QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;

// Biên lai web (spec thanh toán trực tuyến §3.4) cho mọi dòng LichSuThanhToan chưa xóa mềm. Phần gửi lại biên lai làm ở phần sau.
public interface IInvoiceReceiptService
{
    // Cổng khách thuê: nguoiThueId lấy từ claim; biên lai không thuộc khách hoặc của dòng đã hủy ghi nhận trả NotFound.
    Task<ServiceResult<InvoiceReceiptDto>> GetForTenantAsync(int lichSuThanhToanId, int nguoiThueId, CancellationToken ct = default);

    // Cổng quản lý: kiểm quyền xem hóa đơn của chi nhánh. actorId không hợp lệ hoặc không có quyền trả Forbidden.
    Task<ServiceResult<InvoiceReceiptDto>> GetForEmployeeAsync(int actorId, int lichSuThanhToanId, CancellationToken ct = default);
}
