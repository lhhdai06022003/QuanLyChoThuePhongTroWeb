using System.Threading;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Common.Enums;

namespace QuanLyChoThuePhongTroWeb.Application.Features.NguoiDungs.Services
{
    public interface IUserSessionService
    {
        Task<bool> IsSessionValidAsync(int nguoiDungId, AppRole role, CancellationToken cancellationToken = default);
    }
}
