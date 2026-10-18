using System.Threading;
using System.Threading.Tasks;

namespace QuanLyChoThuePhongTroWeb.Application.Abstractions.Notifications
{
    public interface IThongBaoNotifier
    {
        Task SendToUserAsync(int nguoiDungId, object payload, CancellationToken cancellationToken = default);
        Task SendToRoleGroupAsync(string roleName, object payload, CancellationToken cancellationToken = default);

        // Sự kiện "HoaDonThanhToanCapNhat" cho panel thanh toán của khách thuê (spec thanh toán trực tuyến §3.3).
        // Có cài mặc định để các bản cài đặt khác (test) không phải sửa.
        Task SendInvoicePaymentUpdatedAsync(int nguoiDungId, InvoicePaymentUpdatedEvent payload, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
