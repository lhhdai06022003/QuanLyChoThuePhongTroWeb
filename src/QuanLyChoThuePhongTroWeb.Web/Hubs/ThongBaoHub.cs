using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;
using System.Threading.Tasks;

namespace QuanLyChoThuePhongTroWeb.Hubs
{
    // Chỉ nhận kết nối đã đăng nhập (spec bảo mật xác thực §2.7).
    [Authorize]
    public class ThongBaoHub : Hub
    {
        public override async Task OnConnectedAsync()
        {
            var role = Context.User?.FindFirst(ClaimTypes.Role)?.Value;
            if (!string.IsNullOrEmpty(role))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, role);
            }
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(System.Exception? exception)
        {
            var role = Context.User?.FindFirst(ClaimTypes.Role)?.Value;
            if (!string.IsNullOrEmpty(role))
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, role);
            }
            await base.OnDisconnectedAsync(exception);
        }
    }
}
