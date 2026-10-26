using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Notifications;
using QuanLyChoThuePhongTroWeb.Hubs;

namespace QuanLyChoThuePhongTroWeb.Web.Hubs
{
    public class SignalRThongBaoNotifier : IThongBaoNotifier
    {
        private readonly IHubContext<ThongBaoHub> _hubContext;

        public SignalRThongBaoNotifier(IHubContext<ThongBaoHub> hubContext)
        {
            _hubContext = hubContext;
        }

        public async Task SendToUserAsync(int nguoiDungId, object payload, CancellationToken cancellationToken = default)
        {
            await _hubContext.Clients.User(nguoiDungId.ToString()).SendAsync("NhanThongBao", payload, cancellationToken);
        }

        public async Task SendToRoleGroupAsync(string roleName, object payload, CancellationToken cancellationToken = default)
        {
            await _hubContext.Clients.Group(roleName).SendAsync("NhanThongBao", payload, cancellationToken);
        }

        // Panel thanh toán của khách (spec thanh toán trực tuyến §3.3) lắng nghe sự kiện này trên kết nối ThongBaoHub sẵn có.
        public async Task SendInvoicePaymentUpdatedAsync(int nguoiDungId, InvoicePaymentUpdatedEvent payload, CancellationToken cancellationToken = default)
        {
            await _hubContext.Clients.User(nguoiDungId.ToString()).SendAsync(
                "HoaDonThanhToanCapNhat",
                new { hoaDonId = payload.HoaDonId, conLai = payload.ConLai, trangThai = payload.TrangThai },
                cancellationToken);
        }

        // Chưa có trang nào nghe sự kiện này (trang đặt cọc của khách nghe ở phần sau của đợt giữ chỗ).
        public async Task SendReservationPaymentUpdatedAsync(int nguoiDungId, ReservationPaymentUpdatedEvent payload, CancellationToken cancellationToken = default)
        {
            await _hubContext.Clients.User(nguoiDungId.ToString()).SendAsync(
                "GiuChoThanhToanCapNhat",
                new { yeuCauGiuChoId = payload.YeuCauGiuChoId, daNhan = payload.DaNhan, conLai = payload.ConLai, trangThai = payload.TrangThai },
                cancellationToken);
        }
    }
}
