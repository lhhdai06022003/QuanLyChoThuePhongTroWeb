using System.Threading;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Features.NguoiDungs.DTOs;

namespace QuanLyChoThuePhongTroWeb.Application.Features.NguoiDungs.Persistence
{
    public interface IUserSessionStore
    {
        Task<UserSessionSnapshotDto?> GetSessionSnapshotAsync(int nguoiDungId, CancellationToken ct = default);
    }
}
