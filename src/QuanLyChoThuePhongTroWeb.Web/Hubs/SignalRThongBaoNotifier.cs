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
    }
}
