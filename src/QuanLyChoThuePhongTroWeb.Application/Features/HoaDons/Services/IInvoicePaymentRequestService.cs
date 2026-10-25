using System.Threading;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Services;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;

namespace QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Services
{
    // Phía khách thuê. Từ spec thanh toán trực tuyến §8 khách không còn tự tạo lượt, hủy lượt hay nộp minh chứng
    // (panel, QR và biên lai nằm ở ITenantPaymentPanelService / IInvoiceReceiptService), nên chỉ còn đọc ảnh minh chứng
    // của các lượt cũ. nguoiThueId lấy từ claim; ảnh không thuộc khách trả NotFound để không lộ sự tồn tại.
    public interface IInvoicePaymentRequestService
    {
        Task<ServiceResult<MeterImageReadResult>> GetProofImageAsync(int minhChungId, int nguoiThueId, CancellationToken ct = default);
    }
}
