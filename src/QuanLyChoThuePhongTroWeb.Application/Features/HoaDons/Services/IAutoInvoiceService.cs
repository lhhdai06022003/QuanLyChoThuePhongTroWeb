using System.Collections.Generic;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.DTOs;

namespace QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Services
{
    public interface IAutoInvoiceService
    {
        // actorId là người làm thao tác gốc (nhân viên/Admin, hoặc tài khoản khách khi trigger = KhachTaiAnh).
        // Luôn trả Success = true với kết quả trong Data, không ném exception.
        Task<ServiceResult<AutoInvoiceRunResult>> RunForRoomAsync(int actorId, AutoInvoiceTrigger trigger, int phongTroId, int thang, int nam);

        // Như gọi RunForRoomAsync lần lượt từng phòng (cùng thứ tự, mỗi phòng một kết quả), nhưng đọc ngữ cảnh ban đầu
        // của cả danh sách trong một lượt. Mỗi phòng vẫn được đánh giá lại riêng ngay trước khi tạo hóa đơn.
        Task<IReadOnlyList<ServiceResult<AutoInvoiceRunResult>>> RunForRoomsAsync(int actorId, AutoInvoiceTrigger trigger, IReadOnlyList<int> phongTroIds, int thang, int nam);
    }
}
