using QuanLyChoThuePhongTroWeb.Application.Common.Models;

namespace QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;

// Đồng bộ bù giao dịch tiền vào qua API SePay khi webhook bị lỡ (spec thanh toán trực tuyến §4.3). Chỉ Admin, fail closed.
// Không dùng property trên interface: BranchScopeArchitectureTests quét cả getter.
public interface ISePaySyncService
{
    // Thông tin để dựng màn: đã cấu hình API token chưa, khoảng ngày mặc định và tối đa, có đang ở chế độ chỉ đối chiếu không.
    Task<ServiceResult<SePaySyncSettingsDto>> GetSettingsAsync(int actorId, CancellationToken ct = default);

    // Duyệt hết các trang tiền vào trong khoảng ngày rồi đưa từng giao dịch qua IBankTransferIngestionService (cùng khóa chống trùng
    // với webhook). Dừng giữa chừng vẫn trả các dòng đã xử lý (HoanTat = false). Không phải Admin thì không gọi API SePay.
    Task<ServiceResult<SePaySyncResult>> SyncAsync(int actorId, SePaySyncRequest request, CancellationToken ct = default);
}
