using QuanLyChoThuePhongTroWeb.Application.Common.Models;

namespace QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;

// Biên lai web (spec thanh toán trực tuyến §3.4) cho mọi dòng LichSuThanhToan chưa xóa mềm, và gửi lại email biên lai (§6.3, §7.2).
public interface IInvoiceReceiptService
{
    // Cổng khách thuê: nguoiThueId lấy từ claim; biên lai không thuộc khách hoặc của dòng đã hủy ghi nhận trả NotFound.
    Task<ServiceResult<InvoiceReceiptDto>> GetForTenantAsync(int lichSuThanhToanId, int nguoiThueId, CancellationToken ct = default);

    // Cổng quản lý: kiểm quyền xem hóa đơn của chi nhánh. actorId không hợp lệ hoặc không có quyền trả Forbidden.
    Task<ServiceResult<InvoiceReceiptDto>> GetForEmployeeAsync(int actorId, int lichSuThanhToanId, CancellationToken ct = default);

    // Cổng quản lý: gửi lại email biên lai cho khách đứng tên hợp đồng. Quyền InvoiceSend theo chi nhánh; dòng đã hủy ghi nhận,
    // khách không có email hợp lệ hoặc gửi thư lỗi trả lỗi. Gửi ngay trong request, không qua hàng đợi.
    Task<ServiceResult<InvoiceReceiptResendResult>> ResendForEmployeeAsync(int actorId, int lichSuThanhToanId, CancellationToken ct = default);
}
