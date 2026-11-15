using System.Threading;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Features.ViecCanLam.DTOs;

namespace QuanLyChoThuePhongTroWeb.Application.Features.ViecCanLam.Services
{
    public interface IViecCanLamService
    {
        // Việc suy ra từ dữ liệu, giới hạn theo chi nhánh của actor; không lọc theo tháng/năm.
        Task<ViecCanLamTongHopDto> GetTongHopAsync(int actorId, int? branchId, CancellationToken ct = default);

        // Bảng hóa đơn quá hạn. tongSoDaDem: tổng số hóa đơn quá hạn mà người gọi đã có từ GetTongHopAsync cùng actor và cùng
        // chi nhánh trong request này (SoLuong của mục TT2); truyền vào thì không đếm lại, để null thì service tự đếm.
        // Phạm vi chi nhánh vẫn được kiểm như thường: ngoài phạm vi thì trả rỗng, bỏ qua số truyền vào.
        Task<DanhSachHoaDonQuaHanDto> GetHoaDonQuaHanAsync(int actorId, int? branchId, int? tongSoDaDem = null, CancellationToken ct = default);
    }
}
