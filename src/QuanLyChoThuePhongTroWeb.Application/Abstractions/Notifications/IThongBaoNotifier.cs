using System.Threading;
using System.Threading.Tasks;

namespace QuanLyChoThuePhongTroWeb.Application.Abstractions.Notifications
{
    public interface IThongBaoNotifier
    {
        Task SendToUserAsync(int nguoiDungId, object payload, CancellationToken cancellationToken = default);
        Task SendToRoleGroupAsync(string roleName, object payload, CancellationToken cancellationToken = default);
    }
}
